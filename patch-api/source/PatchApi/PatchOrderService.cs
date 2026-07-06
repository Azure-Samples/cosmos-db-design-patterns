using Microsoft.Azure.Cosmos;

namespace Cosmos.PatchApi;

/// <summary>
/// Demonstrates the Azure Cosmos DB <b>Patch API</b> (partial document update) on a single order that
/// several services update independently.
///
/// <para>Every mutating call here is a <see cref="Container.PatchItemAsync{T}(string, PartitionKey,
/// IReadOnlyList{PatchOperation}, PatchItemRequestOptions, System.Threading.CancellationToken)"/>:
/// the client sends only the <em>operations</em> (set/increment/add/remove) — not the whole document
/// — and the server applies them in place. That means:</para>
/// <list type="bullet">
///   <item>No read-modify-write round trip (fewer RUs, lower latency).</item>
///   <item>No lost updates when two callers change <em>different</em> fields.</item>
///   <item>A smaller network payload (just the delta), and atomic multi-operation updates.</item>
/// </list>
/// </summary>
public sealed class PatchOrderService : IAsyncDisposable
{
    private const string DatabaseName = "PatchApiDB";
    private const string ContainerName = "OrderStore";

    /// <summary>The single order the whole demo mutates, so the effect of each patch is easy to see.</summary>
    public const string DemoOrderId = "ORD-1001";

    private readonly string _endpoint;
    private readonly string? _key;

    private readonly LinkedList<LogEntry> _log = new();
    private readonly object _logSync = new();

    private CosmosClient? _client;
    private Container? _container;
    private int _started;

    public event Action? Changed;

    public PatchOrderService(string endpoint, string? key)
    {
        _endpoint = endpoint;
        _key = key;
    }

    public bool IsReady => Volatile.Read(ref _started) == 2;

    public async Task EnsureStartedAsync()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            _client = CosmosClientFactory.Create(_endpoint, _key);
            Database db = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cts.Token);
            _container = await db.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = ContainerName,
                PartitionKeyPath = "/orderId",
            }, cancellationToken: cts.Token);

            await SeedDemoOrderAsync();
            Volatile.Write(ref _started, 2);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Could not reach Azure Cosmos DB at {_endpoint}. If you're running locally, start the emulator with 'docker compose up -d' from the repo root, then reload. ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    private static Order FreshDemoOrder() => new()
    {
        Id = DemoOrderId,
        OrderId = DemoOrderId,
        Customer = "Ada Lovelace",
        Product = "Mechanical Keyboard",
        Amount = 89m,
        PaymentStatus = "Pending",
        ShippingStatus = "NotShipped",
        TrackingNumber = null,
        ViewCount = 0,
        Tags = new List<string>(),
        LastWriteBy = "seed",
    };

    private async Task SeedDemoOrderAsync()
    {
        await _container!.UpsertItemAsync(FreshDemoOrder(), new PartitionKey(DemoOrderId));
    }

    /// <summary>Reads the demo order. Returns the document and the RU the read cost.</summary>
    public async Task<(Order Order, double RequestCharge)> GetOrderAsync()
    {
        ItemResponse<Order> resp = await _container!.ReadItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId));
        return (resp.Resource, resp.RequestCharge);
    }

    // ---- Individual patch operations. Each one sends ONLY its operation — no read first. --------

    /// <summary>Payment service: set the payment status (<c>Set</c>).</summary>
    public Task<double> SetPaymentStatusAsync(string status) =>
        PatchAsync($"Payment service set /paymentStatus = \"{status}\".",
            PatchOperation.Set("/paymentStatus", status),
            PatchOperation.Set("/lastWriteBy", "payment"));

    /// <summary>Shipping service: set the shipping status and tracking number in one atomic patch.</summary>
    public Task<double> SetShippingAsync(string status, string trackingNumber) =>
        PatchAsync($"Shipping service set /shippingStatus = \"{status}\" and /trackingNumber = \"{trackingNumber}\" (one atomic patch).",
            PatchOperation.Set("/shippingStatus", status),
            PatchOperation.Set("/trackingNumber", trackingNumber),
            PatchOperation.Set("/lastWriteBy", "shipping"));

    /// <summary>Analytics service: increment the view counter (<c>Increment</c>) — no read needed.</summary>
    public Task<double> IncrementViewsAsync(int by) =>
        PatchAsync($"Analytics service incremented /viewCount by {by} (no read).",
            PatchOperation.Increment("/viewCount", by),
            PatchOperation.Set("/lastWriteBy", "analytics"));

    /// <summary>Merchandising service: append a tag to the array (<c>Add</c> to <c>/tags/-</c>).</summary>
    public Task<double> AddTagAsync(string tag) =>
        PatchAsync($"Merchandising service appended \"{tag}\" to /tags.",
            PatchOperation.Add("/tags/-", tag),
            PatchOperation.Set("/lastWriteBy", "merchandising"));

    /// <summary>Remove the tracking number field (<c>Remove</c>).</summary>
    public Task<double> RemoveTrackingAsync() =>
        PatchAsync("Removed /trackingNumber.",
            PatchOperation.Remove("/trackingNumber"),
            PatchOperation.Set("/lastWriteBy", "shipping"));

    private async Task<double> PatchAsync(string message, params PatchOperation[] operations)
    {
        ItemResponse<Order> resp = await _container!.PatchItemAsync<Order>(
            DemoOrderId, new PartitionKey(DemoOrderId), operations);
        Log($"{message}  ({resp.RequestCharge:0.##} RU)", LogLevel.Success);
        Changed?.Invoke();
        return resp.RequestCharge;
    }

    // ---- RU comparison: the SAME logical update (mark the order paid) two ways. -----------------

    /// <summary>
    /// Applies the same change (set <c>paymentStatus = "Paid"</c>) once with a single Patch and once
    /// with a read-modify-write + full replace, returning the RU cost of each so the difference is
    /// visible. On the emulator a point read, a replace, and a patch are each ~1 RU, so Patch (1 RU)
    /// beats read-modify-write (~2 RU) by skipping the read. In production Patch also avoids
    /// rewriting the entire document, which is a much larger saving on big documents.
    /// </summary>
    public async Task<RuComparison> CompareUpdateAsync()
    {
        // Approach 1 — Patch: send only the operation, no read.
        ItemResponse<Order> patchResp = await _container!.PatchItemAsync<Order>(
            DemoOrderId, new PartitionKey(DemoOrderId),
            new[] { PatchOperation.Set("/paymentStatus", "Paid") });
        double patchRu = patchResp.RequestCharge;

        // Approach 2 — Read-modify-write: read the whole document, change one field, replace it all.
        ItemResponse<Order> readResp = await _container.ReadItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId));
        double readRu = readResp.RequestCharge;
        Order doc = readResp.Resource;
        doc.PaymentStatus = "Paid";
        ItemResponse<Order> replaceResp = await _container.ReplaceItemAsync(doc, DemoOrderId, new PartitionKey(DemoOrderId));
        double replaceRu = replaceResp.RequestCharge;

        var cmp = new RuComparison(patchRu, readRu, replaceRu);
        Log($"RU comparison — Patch: {patchRu:0.##} RU vs Read ({readRu:0.##}) + Replace ({replaceRu:0.##}) = {cmp.ReadModifyWriteRu:0.##} RU.", LogLevel.Info);
        Changed?.Invoke();
        return cmp;
    }

    // ---- Concurrency race: two services update DIFFERENT fields at the same time. ---------------

    /// <summary>
    /// Runs two services concurrently — payment sets <c>paymentStatus = "Paid"</c>, shipping sets
    /// <c>shippingStatus = "Shipped"</c>. With <see cref="RaceMode.ReadModifyWrite"/> both read the
    /// same document and the second full replace overwrites the first field (a lost update). With
    /// <see cref="RaceMode.Patch"/> each patches only its own field, so both survive.
    /// </summary>
    public async Task<RaceResult> RunRaceAsync(RaceMode mode)
    {
        // Start each race from the same clean state.
        await _container!.ReplaceItemAsync(FreshDemoOrder(), DemoOrderId, new PartitionKey(DemoOrderId));
        Log($"--- Concurrency race ({mode}) — payment and shipping update the order at the same time. ---", LogLevel.Info);

        if (mode == RaceMode.ReadModifyWrite)
        {
            // Both services read the SAME version of the document (the classic race window)...
            Order paymentView = (await _container.ReadItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId))).Resource;
            Order shippingView = (await _container.ReadItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId))).Resource;

            // ...each changes only its own field on its own copy...
            paymentView.PaymentStatus = "Paid";
            paymentView.LastWriteBy = "payment";
            shippingView.ShippingStatus = "Shipped";
            shippingView.LastWriteBy = "shipping";

            // ...and each writes the WHOLE document back. The second replace wins and clobbers the
            // first writer's field, even though they never touched the same field.
            await _container.ReplaceItemAsync(paymentView, DemoOrderId, new PartitionKey(DemoOrderId));
            Log("Payment replaced the whole document (paymentStatus=Paid, but shippingStatus still NotShipped from its stale read).", LogLevel.Warn);
            await _container.ReplaceItemAsync(shippingView, DemoOrderId, new PartitionKey(DemoOrderId));
            Log("Shipping replaced the whole document (shippingStatus=Shipped, but paymentStatus reset to Pending from ITS stale read). Payment's update is LOST.", LogLevel.Error);
        }
        else
        {
            // Each service patches ONLY its own field — no read, nothing to overwrite.
            await Task.WhenAll(
                _container.PatchItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId),
                    new[] { PatchOperation.Set("/paymentStatus", "Paid"), PatchOperation.Set("/lastWriteBy", "payment") }),
                _container.PatchItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId),
                    new[] { PatchOperation.Set("/shippingStatus", "Shipped"), PatchOperation.Set("/trackingNumber", "1Z-RACE-42") }));
            Log("Payment patched /paymentStatus and shipping patched /shippingStatus. Neither read the document; both updates survive.", LogLevel.Success);
        }

        Order final = (await _container.ReadItemAsync<Order>(DemoOrderId, new PartitionKey(DemoOrderId))).Resource;
        bool paymentLost = final.PaymentStatus != "Paid";
        bool shippingLost = final.ShippingStatus != "Shipped";
        var result = new RaceResult(mode, final.PaymentStatus, final.ShippingStatus, paymentLost, shippingLost);

        Log(result.AnyLost
                ? $"Result: paymentStatus=\"{final.PaymentStatus}\", shippingStatus=\"{final.ShippingStatus}\" — an update was LOST."
                : $"Result: paymentStatus=\"{final.PaymentStatus}\", shippingStatus=\"{final.ShippingStatus}\" — both updates preserved.",
            result.AnyLost ? LogLevel.Error : LogLevel.Success);
        Changed?.Invoke();
        return result;
    }

    /// <summary>Resets the demo order to its initial state and clears the timeline.</summary>
    public async Task ResetAsync()
    {
        await SeedDemoOrderAsync();
        lock (_logSync) { _log.Clear(); }
        Log("Reset — restored the order to its initial state.", LogLevel.Info);
        Changed?.Invoke();
    }

    public IReadOnlyList<LogEntry> Timeline()
    {
        lock (_logSync)
        {
            return _log.ToList();
        }
    }

    private void Log(string message, LogLevel level)
    {
        lock (_logSync)
        {
            _log.AddFirst(new LogEntry(DateTimeOffset.UtcNow, message, level));
            while (_log.Count > 60) _log.RemoveLast();
        }
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
