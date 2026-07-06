using System.Net;
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
    /// <c>shippingStatus = "Shipped"</c>. They change DIFFERENT fields, yet the read-modify-write
    /// approaches still collide:
    /// <list type="bullet">
    ///   <item><see cref="RaceMode.ReadModifyWrite"/> — both read the same version and the second
    ///   full replace overwrites the first field (a silent lost update).</item>
    ///   <item><see cref="RaceMode.ReadModifyWriteWithETag"/> — the second replace hits a 412
    ///   conflict (because the ETag guards the whole document, not just the field it changed) and
    ///   must re-read and retry. Correct, but with a needless conflict and extra RUs.</item>
    ///   <item><see cref="RaceMode.Patch"/> — each patches only its own field, so there is no read,
    ///   no conflict, and no lost update.</item>
    /// </list>
    /// The returned <see cref="RaceResult"/> carries the measured RU cost and conflict count.
    /// </summary>
    public async Task<RaceResult> RunRaceAsync(RaceMode mode)
    {
        var pk = new PartitionKey(DemoOrderId);

        // Start each race from the same clean state.
        await _container!.ReplaceItemAsync(FreshDemoOrder(), DemoOrderId, pk);
        Log($"--- Concurrency race ({Describe(mode)}) — payment and shipping update the order at the same time. ---", LogLevel.Info);

        double ru = 0;
        int conflicts = 0;

        if (mode == RaceMode.Patch)
        {
            // Each service patches ONLY its own field — no read, nothing to overwrite.
            ItemResponse<Order> p = await _container.PatchItemAsync<Order>(DemoOrderId, pk,
                new[] { PatchOperation.Set("/paymentStatus", "Paid"), PatchOperation.Set("/lastWriteBy", "payment") });
            ItemResponse<Order> s = await _container.PatchItemAsync<Order>(DemoOrderId, pk,
                new[] { PatchOperation.Set("/shippingStatus", "Shipped"), PatchOperation.Set("/trackingNumber", "1Z-RACE-42") });
            ru += p.RequestCharge + s.RequestCharge;
            Log($"Payment patched /paymentStatus and shipping patched /shippingStatus — no read, no conflict. ({ru:0.##} RU).", LogLevel.Success);
        }
        else if (mode == RaceMode.ReadModifyWrite)
        {
            // Both services read the SAME version of the document...
            ItemResponse<Order> paymentRead = await _container.ReadItemAsync<Order>(DemoOrderId, pk);
            ItemResponse<Order> shippingRead = await _container.ReadItemAsync<Order>(DemoOrderId, pk);
            ru += paymentRead.RequestCharge + shippingRead.RequestCharge;

            Order paymentView = paymentRead.Resource; paymentView.PaymentStatus = "Paid"; paymentView.LastWriteBy = "payment";
            Order shippingView = shippingRead.Resource; shippingView.ShippingStatus = "Shipped"; shippingView.LastWriteBy = "shipping";

            // ...and each writes the WHOLE document back with no precondition. The second replace wins.
            ru += (await _container.ReplaceItemAsync(paymentView, DemoOrderId, pk)).RequestCharge;
            Log("Payment replaced the whole document (no ETag): paymentStatus=Paid.", LogLevel.Warn);
            ru += (await _container.ReplaceItemAsync(shippingView, DemoOrderId, pk)).RequestCharge;
            Log($"Shipping replaced the whole document from its stale read: shippingStatus=Shipped, paymentStatus reset to Pending. Payment's update is LOST. ({ru:0.##} RU).", LogLevel.Error);
        }
        else // ReadModifyWriteWithETag
        {
            // Both services read the SAME version — capturing the same ETag.
            ItemResponse<Order> paymentRead = await _container.ReadItemAsync<Order>(DemoOrderId, pk);
            ItemResponse<Order> shippingRead = await _container.ReadItemAsync<Order>(DemoOrderId, pk);
            ru += paymentRead.RequestCharge + shippingRead.RequestCharge;

            // Payment writes first with its ETag — the document still matches, so it succeeds.
            Order paymentView = paymentRead.Resource; paymentView.PaymentStatus = "Paid"; paymentView.LastWriteBy = "payment";
            ru += (await _container.ReplaceItemAsync(paymentView, DemoOrderId, pk,
                new ItemRequestOptions { IfMatchEtag = paymentRead.ETag })).RequestCharge;
            Log("Payment replaced with IfMatchEtag — ETag still matched, so it succeeded.", LogLevel.Info);

            // Shipping tries with its now-stale ETag. It only changed shippingStatus, but the ETag
            // guards the WHOLE document, so payment's write invalidated it: 412 Precondition Failed.
            Order shippingView = shippingRead.Resource; shippingView.ShippingStatus = "Shipped"; shippingView.LastWriteBy = "shipping";
            try
            {
                ru += (await _container.ReplaceItemAsync(shippingView, DemoOrderId, pk,
                    new ItemRequestOptions { IfMatchEtag = shippingRead.ETag })).RequestCharge;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                conflicts++;
                ru += ex.RequestCharge;
                Log($"Shipping's IfMatchEtag replace hit a 412 CONFLICT — even though it only changed shippingStatus, the ETag guards the whole document. That RU is wasted. ({ex.RequestCharge:0.##} RU).", LogLevel.Warn);

                // Retry: re-read to get the current ETag, re-apply the change, replace again.
                ItemResponse<Order> retryRead = await _container.ReadItemAsync<Order>(DemoOrderId, pk);
                ru += retryRead.RequestCharge;
                Order retryView = retryRead.Resource; retryView.ShippingStatus = "Shipped"; retryView.LastWriteBy = "shipping";
                ru += (await _container.ReplaceItemAsync(retryView, DemoOrderId, pk,
                    new ItemRequestOptions { IfMatchEtag = retryRead.ETag })).RequestCharge;
                Log($"Shipping re-read and retried — succeeded. Result is correct, but it cost an extra read + replace + the failed 412. ({ru:0.##} RU total).", LogLevel.Success);
            }
        }

        // Final read is instrumentation only (to display state); its RU is not counted in the update cost.
        Order final = (await _container.ReadItemAsync<Order>(DemoOrderId, pk)).Resource;
        bool paymentLost = final.PaymentStatus != "Paid";
        bool shippingLost = final.ShippingStatus != "Shipped";
        var result = new RaceResult(mode, final.PaymentStatus, final.ShippingStatus, paymentLost, shippingLost, conflicts, ru);

        Log(result.AnyLost
                ? $"Result: paymentStatus=\"{final.PaymentStatus}\", shippingStatus=\"{final.ShippingStatus}\" — an update was LOST. Cost {ru:0.##} RU."
                : $"Result: paymentStatus=\"{final.PaymentStatus}\", shippingStatus=\"{final.ShippingStatus}\" — both preserved. {conflicts} conflict(s), {ru:0.##} RU.",
            result.AnyLost ? LogLevel.Error : LogLevel.Success);
        Changed?.Invoke();
        return result;
    }

    private static string Describe(RaceMode mode) => mode switch
    {
        RaceMode.ReadModifyWrite => "read-modify-write, no ETag",
        RaceMode.ReadModifyWriteWithETag => "read-modify-write + ETag",
        RaceMode.Patch => "Patch",
        _ => mode.ToString(),
    };

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
