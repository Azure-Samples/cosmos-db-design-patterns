using System.Net;
using Microsoft.Azure.Cosmos;

namespace Cosmos.HierarchicalPartitionKey;

/// <summary>
/// Stores activity events two ways so the pattern can be compared:
/// <list type="bullet">
///   <item><b>Events</b> — a <b>hierarchical</b> partition key (<c>/tenantId</c> then <c>/userId</c>).</item>
///   <item><b>EventsByUser</b> — a single partition key (<c>/userId</c>), the naive "before".</item>
/// </list>
/// The methods run the reads/queries a real multi-tenant app performs and report, for each, the RU
/// charge and whether Cosmos DB could <b>target</b> specific partitions or had to <b>fan out</b>
/// across all of them. (On the single-partition emulator the RU numbers look similar; the targeting
/// difference is what matters, and it becomes a cost/throughput difference at production scale.)
/// </summary>
public sealed class ActivityStore
{
    private static readonly string[] Actions = { "login", "view", "click", "purchase", "logout" };

    private readonly Container _hpk;
    private readonly Container _single;
    private readonly HpkOptions _options;

    public ActivityStore(CosmosClient client, HpkOptions options)
    {
        _options = options;
        _hpk = client.GetContainer(options.DatabaseName, options.HierarchicalContainer);
        _single = client.GetContainer(options.DatabaseName, options.SingleKeyContainer);
    }

    /// <summary>Creates the database and both containers (hierarchical + single-key) if missing.</summary>
    public static async Task EnsureContainersAsync(CosmosClient client, HpkOptions options, CancellationToken cancellationToken = default)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(options.DatabaseName, cancellationToken: cancellationToken);

        // A hierarchical partition key is declared simply by giving MULTIPLE paths (up to three).
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.HierarchicalContainer,
            PartitionKeyPaths = new List<string> { "/tenantId", "/userId" },
        }, cancellationToken: cancellationToken);

        await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.SingleKeyContainer,
            PartitionKeyPath = "/userId",
        }, cancellationToken: cancellationToken);
    }

    public async Task<int> CountAsync()
    {
        using FeedIterator<int> feed = _hpk.GetItemQueryIterator<int>("SELECT VALUE COUNT(1) FROM c");
        return feed.HasMoreResults ? (await feed.ReadNextAsync()).First() : 0;
    }

    /// <summary>Seeds identical data into both containers.</summary>
    public async Task SeedAsync(int tenants, int usersPerTenant, int eventsPerUser)
    {
        var rng = new Random(42);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int t = 0; t < tenants; t++)
        {
            string tenantId = $"tenant-{t:00}";
            for (int u = 0; u < usersPerTenant; u++)
            {
                string userId = $"user-{t:00}-{u:00}";
                for (int e = 0; e < eventsPerUser; e++)
                {
                    var ev = new ActivityEvent
                    {
                        Id = $"{tenantId}:{userId}:{e:000}",
                        TenantId = tenantId,
                        UserId = userId,
                        Action = Actions[rng.Next(Actions.Length)],
                        Timestamp = now.AddMinutes(-rng.Next(0, 100_000)),
                    };

                    await _hpk.UpsertItemAsync(ev, new PartitionKeyBuilder().Add(ev.TenantId).Add(ev.UserId).Build());
                    await _single.UpsertItemAsync(ev, new PartitionKey(ev.UserId));
                }
            }
        }
    }

    /// <summary>Returns the tenant → user → count tree, showing how data maps to the hierarchy.</summary>
    public async Task<List<TenantNode>> DistributionAsync()
    {
        var tenants = new Dictionary<string, TenantNode>();
        using FeedIterator<GroupRow> feed = _hpk.GetItemQueryIterator<GroupRow>(
            "SELECT c.tenantId, c.userId, COUNT(1) AS n FROM c GROUP BY c.tenantId, c.userId");
        while (feed.HasMoreResults)
        {
            foreach (GroupRow row in await feed.ReadNextAsync())
            {
                if (!tenants.TryGetValue(row.TenantId, out TenantNode? node))
                {
                    node = new TenantNode { TenantId = row.TenantId };
                    tenants[row.TenantId] = node;
                }
                node.Users.Add(new UserNode { UserId = row.UserId, Events = row.N });
                node.TotalEvents += row.N;
            }
        }

        return tenants.Values
            .OrderBy(t => t.TenantId)
            .Select(t => new TenantNode
            {
                TenantId = t.TenantId,
                TotalEvents = t.TotalEvents,
                Users = t.Users.OrderBy(u => u.UserId).ToList()
            })
            .ToList();
    }

    private sealed class GroupRow
    {
        [Newtonsoft.Json.JsonProperty("tenantId")] public string TenantId { get; set; } = string.Empty;
        [Newtonsoft.Json.JsonProperty("userId")] public string UserId { get; set; } = string.Empty;
        [Newtonsoft.Json.JsonProperty("n")] public int N { get; set; }
    }

    // ---- The reads/queries, each returning what Cosmos did + a teaching note. ----

    /// <summary>Point read with the FULL hierarchical key — the cheapest, most targeted operation.</summary>
    public async Task<QueryResult> PointReadAsync(string tenantId, string userId)
    {
        string id = $"{tenantId}:{userId}:000";
        PartitionKey pk = new PartitionKeyBuilder().Add(tenantId).Add(userId).Build();
        try
        {
            ItemResponse<ActivityEvent> resp = await _hpk.ReadItemAsync<ActivityEvent>(id, pk);
            return new QueryResult
            {
                Kind = QueryKind.PointRead,
                Title = "Point read (full key)",
                Description = "Read one event by id with the complete partition key.",
                QueryText = $"ReadItem(id: \"{id}\")",
                PartitionKeyUsed = $"[{tenantId}, {userId}]",
                Targeted = true,
                RequestCharge = resp.RequestCharge,
                Count = 1,
                Note = "The full hierarchical key points at exactly one physical partition — the fastest, cheapest read Cosmos DB offers.",
                Items = new List<ActivityEvent> { resp.Resource },
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new QueryResult
            {
                Kind = QueryKind.PointRead,
                Title = "Point read (full key)",
                QueryText = $"ReadItem(id: \"{id}\")",
                PartitionKeyUsed = $"[{tenantId}, {userId}]",
                Note = "No event with that id.",
            };
        }
    }

    /// <summary>Tenant dashboard: filter on the FIRST key level (<c>/tenantId</c>).</summary>
    public Task<QueryResult> TenantPrefixAsync(string tenantId) =>
        RunAsync(_hpk,
            new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t").WithParameter("@t", tenantId),
            new QueryRequestOptions { PartitionKey = new PartitionKeyBuilder().Add(tenantId).Build() },
            QueryKind.TenantPrefix,
            "Tenant dashboard (prefix query)",
            "All events for a tenant, filtered on the first key level.",
            $"[{tenantId}]  (prefix)",
            targeted: true,
            "Because tenantId is the FIRST key level, this predicate lets Cosmos DB prune to only that tenant's sub-partitions — no fan-out. This is the everyday query a multi-tenant app runs, and the hierarchical key keeps it targeted no matter how large the tenant grows.");

    /// <summary>User drill-down: filter on BOTH key levels (<c>/tenantId</c> + <c>/userId</c>).</summary>
    public Task<QueryResult> TenantAndUserAsync(string tenantId, string userId) =>
        RunAsync(_hpk,
            new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t AND c.userId = @u")
                .WithParameter("@t", tenantId).WithParameter("@u", userId),
            new QueryRequestOptions { PartitionKey = new PartitionKeyBuilder().Add(tenantId).Add(userId).Build() },
            QueryKind.TenantAndUser,
            "User drill-down (full key)",
            "All events for one user within a tenant.",
            $"[{tenantId}, {userId}]",
            targeted: true,
            "Filtering on both key levels targets a single sub-partition — as cheap as a query gets.");

    /// <summary>An analytics query on a NON-key field (action) across every tenant — a fan-out.</summary>
    public Task<QueryResult> CrossPartitionByActionAsync(string action) =>
        RunAsync(_hpk,
            new QueryDefinition("SELECT * FROM c WHERE c.action = @action").WithParameter("@action", action),
            new QueryRequestOptions(),
            QueryKind.CrossPartition,
            "Cross-tenant analytics (fan-out)",
            "Filter on a non-key field across all tenants.",
            "none",
            targeted: false,
            "No partition key is supplied and the filter isn't the key, so Cosmos DB must fan out to EVERY partition. At production scale this is where cost and latency climb with the number of partitions.");

    /// <summary>The "before": the same tenant dashboard on the SINGLE-key container — a fan-out.</summary>
    public Task<QueryResult> SingleKeyTenantAsync(string tenantId) =>
        RunAsync(_single,
            new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t").WithParameter("@t", tenantId),
            new QueryRequestOptions(),
            QueryKind.SingleKeyCrossPartition,
            "Tenant dashboard on /userId (before)",
            "The same tenant query, but the container is partitioned only by /userId.",
            "none (tenantId isn't the key)",
            targeted: false,
            "With a single /userId key, tenantId is not the partition key, so this everyday tenant query fans out across all partitions. Leading the hierarchical key with tenantId is what fixes this.");

    private async Task<QueryResult> RunAsync(
        Container container, QueryDefinition query, QueryRequestOptions options,
        QueryKind kind, string title, string description, string partitionKeyUsed, bool targeted, string note)
    {
        double ru = 0;
        var items = new List<ActivityEvent>();
        using FeedIterator<ActivityEvent> feed = container.GetItemQueryIterator<ActivityEvent>(query, requestOptions: options);
        while (feed.HasMoreResults)
        {
            FeedResponse<ActivityEvent> page = await feed.ReadNextAsync();
            ru += page.RequestCharge;
            items.AddRange(page);
        }

        return new QueryResult
        {
            Kind = kind,
            Title = title,
            Description = description,
            QueryText = query.QueryText,
            PartitionKeyUsed = partitionKeyUsed,
            Targeted = targeted,
            RequestCharge = ru,
            Count = items.Count,
            Note = note,
            Items = items.Take(25).ToList(),
        };
    }
}
