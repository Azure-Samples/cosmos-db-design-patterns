using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.HierarchicalPartitionKey;

// ---------------------------------------------------------------------------
// Model – an activity event with a two-level hierarchical key (/tenantId/userId).
// ---------------------------------------------------------------------------

public class Event
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Integration tests for the Hierarchical Partition Key design pattern.
///
/// The pattern declares a container with a multi-level partition key (here <c>/tenantId</c> then
/// <c>/userId</c>) by supplying multiple partition key paths. These tests verify:
///   - a hierarchical (MultiHash) container can be created and written to with a full key;
///   - a point read with the full hierarchical key returns the item;
///   - a prefix filter on the first level (<c>tenantId</c>) returns just that tenant's items;
///   - a filter on both levels returns just that user's items.
///
/// Note: filtering is done with a WHERE predicate on the key paths. In production that predicate
/// lets Cosmos DB prune partitions (targeted); on the emulator it also returns the correct subset.
/// (Passing a partial partition key in the query request options is not applied as a filter by the
/// emulator, so the predicate is what makes results correct everywhere.)
/// </summary>
public class HierarchicalPartitionKeyTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"HpkTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public HierarchicalPartitionKeyTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));

        // A hierarchical partition key is declared by supplying multiple paths.
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = "Events",
            PartitionKeyPaths = new List<string> { "/tenantId", "/userId" },
        }));

        // 3 tenants x 2 users x 5 events = 30 events (each tenant has 10, each user has 5).
        for (int t = 1; t <= 3; t++)
        {
            for (int u = 1; u <= 2; u++)
            {
                for (int e = 0; e < 5; e++)
                {
                    var ev = new Event { Id = $"t{t}-u{u}-e{e}", TenantId = $"tenant-{t}", UserId = $"user-{t}-{u}" };
                    await _container.UpsertItemAsync(ev, new PartitionKeyBuilder().Add(ev.TenantId).Add(ev.UserId).Build());
                }
            }
        }
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    private async Task<int> CountAsync(string sql, QueryDefinition? query = null)
    {
        int count = 0;
        using FeedIterator<Event> feed = _container.GetItemQueryIterator<Event>(query ?? new QueryDefinition(sql));
        while (feed.HasMoreResults)
        {
            count += (await feed.ReadNextAsync()).Count;
        }
        return count;
    }

    [Fact]
    public async Task PointRead_WithFullHierarchicalKey_ReturnsItem()
    {
        PartitionKey pk = new PartitionKeyBuilder().Add("tenant-2").Add("user-2-1").Build();

        ItemResponse<Event> resp = await _container.ReadItemAsync<Event>("t2-u1-e0", pk);

        Assert.Equal("tenant-2", resp.Resource.TenantId);
        Assert.Equal("user-2-1", resp.Resource.UserId);
    }

    [Fact]
    public async Task TenantPrefixFilter_ReturnsOnlyThatTenant()
    {
        int count = await CountAsync(null!, new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @t").WithParameter("@t", "tenant-2"));

        Assert.Equal(10, count); // 2 users x 5 events
    }

    [Fact]
    public async Task FullKeyFilter_ReturnsOnlyThatUser()
    {
        int count = await CountAsync(null!, new QueryDefinition(
            "SELECT * FROM c WHERE c.tenantId = @t AND c.userId = @u")
            .WithParameter("@t", "tenant-2").WithParameter("@u", "user-2-1"));

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task CrossPartitionCount_SeesAllEvents()
    {
        int count = await CountAsync("SELECT * FROM c");

        Assert.Equal(30, count);
    }
}
