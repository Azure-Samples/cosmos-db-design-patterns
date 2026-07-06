using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace Cosmos.TransactionalOutbox;

/// <summary>
/// Drives the two ways of publishing an event when an order is placed:
///
/// <list type="number">
///   <item><b>Naive dual-write</b> — save the order, then publish the event as a separate step. If
///   the app dies in between, the order exists but the event is lost.</item>
///   <item><b>Transactional outbox</b> — save the order AND an outbox event in a single
///   <see cref="TransactionalBatch"/> (atomic within the order's partition). A change feed processor
///   then relays outbox events downstream. Because the event is committed with the order, it can
///   never be lost — even if the app crashes right after the commit.</item>
/// </list>
///
/// The (simulated) downstream consumer is in-memory and dedupes by event id, since change feed
/// delivery is at-least-once.
/// </summary>
public sealed class OrderOutboxService : IAsyncDisposable
{
    private const string DatabaseName = "OutboxDB";
    private const string ContainerName = "OrderStore";
    private const string LeaseContainerName = "leases";

    private static readonly string[] Customers = { "Ada", "Grace", "Alan", "Katherine", "Linus", "Margaret", "Dennis", "Barbara" };
    private static readonly (string Product, decimal Price)[] Products =
    {
        ("Mechanical Keyboard", 89m), ("Wireless Mouse", 29m), ("4K Monitor", 349m),
        ("USB-C Dock", 129m), ("Noise-Cancelling Headset", 199m), ("Laptop Stand", 39m),
    };

    private readonly string _endpoint;
    private readonly string? _key;

    private readonly ConcurrentDictionary<string, DeliveredEvent> _downstream = new();
    private readonly LinkedList<LogEntry> _log = new();
    private readonly object _logSync = new();

    private CosmosClient? _client;
    private Container? _container;
    private ChangeFeedProcessor? _processor;
    private int _started;
    private long _ordersPlaced;
    private long _eventsLost;

    public event Action? Changed;

    public OrderOutboxService(string endpoint, string? key)
    {
        _endpoint = endpoint;
        _key = key;
    }

    public bool IsReady => Volatile.Read(ref _started) == 2;
    public long OrdersPlaced => Interlocked.Read(ref _ordersPlaced);
    public long EventsDelivered => _downstream.Count;
    public long EventsLost => Interlocked.Read(ref _eventsLost);

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
            await db.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = LeaseContainerName,
                PartitionKeyPath = "/id",
            }, cancellationToken: cts.Token);

            // The relay: reads outbox events from the container and delivers them downstream.
            _processor = _container
                .GetChangeFeedProcessorBuilder<JObject>("outbox-relay", HandleChangesAsync)
                .WithInstanceName($"relay-{Guid.NewGuid():N}")
                .WithLeaseContainer(_client.GetContainer(DatabaseName, LeaseContainerName))
                .WithStartTime(DateTime.MinValue.ToUniversalTime())
                .WithPollInterval(TimeSpan.FromSeconds(1))
                .Build();
            await _processor.StartAsync();

            Volatile.Write(ref _started, 2);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Could not reach Azure Cosmos DB at {_endpoint}. If you're running locally, start the emulator with 'docker compose up -d' from the repo root, then reload. ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    /// <summary>Places an order using the selected mode, optionally simulating a crash mid-flow.</summary>
    public async Task PlaceOrderAsync(OrderMode mode, bool crash)
    {
        var rng = Random.Shared;
        string orderId = $"ORD-{rng.Next(1000, 9999)}-{rng.Next(100, 999)}";
        string customer = Customers[rng.Next(Customers.Length)];
        (string product, decimal price) = Products[rng.Next(Products.Length)];

        var order = new Order { Id = orderId, OrderId = orderId, Customer = customer, Product = product, Amount = price };
        var evt = new OutboxEvent { OrderId = orderId, Customer = customer, Product = product, Amount = price };

        if (mode == OrderMode.NaiveDualWrite)
        {
            await _container!.UpsertItemAsync(order, new PartitionKey(orderId));
            Log($"Order {orderId} saved to Cosmos DB.", LogLevel.Info);

            if (crash)
            {
                Log($"App crashed before the second write — the {evt.EventType} event was never published. Downstream will never hear about {orderId}. EVENT LOST.", LogLevel.Error);
                Interlocked.Increment(ref _eventsLost);
            }
            else
            {
                // The second, separate write — here a direct publish to the downstream consumer.
                Deliver(new DeliveredEvent(evt.Id, orderId, evt.EventType, customer, product, DateTimeOffset.UtcNow), viaOutbox: false);
                Log($"{evt.EventType} event published directly to downstream (second write).", LogLevel.Success);
            }
        }
        else
        {
            TransactionalBatchResponse response = await _container!
                .CreateTransactionalBatch(new PartitionKey(orderId))
                .CreateItem(order)
                .CreateItem(evt)
                .ExecuteAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log($"Transactional batch for {orderId} failed ({(int)response.StatusCode}) — neither the order nor the event was written (all-or-nothing).", LogLevel.Error);
                Changed?.Invoke();
                return;
            }

            Log($"Order {orderId} AND its {evt.EventType} event committed atomically in one transaction.", LogLevel.Success);

            if (crash)
            {
                Log($"App crashed right after the commit — but the event is durably in the outbox. The change feed will still deliver it. Nothing lost.", LogLevel.Warn);
            }
            // No manual publish: the change feed relay delivers the outbox event independently.
        }

        Interlocked.Increment(ref _ordersPlaced);
        Changed?.Invoke();
    }

    // The change feed relay — delivers outbox events (docType == "event") to the downstream consumer.
    private Task HandleChangesAsync(IReadOnlyCollection<JObject> changes, CancellationToken cancellationToken)
    {
        foreach (JObject doc in changes)
        {
            if (!string.Equals(doc["docType"]?.ToString(), "event", StringComparison.Ordinal))
            {
                continue; // Skip the order documents; relay only the outbox events.
            }

            var delivered = new DeliveredEvent(
                doc["id"]?.ToString() ?? string.Empty,
                doc["orderId"]?.ToString() ?? string.Empty,
                doc["eventType"]?.ToString() ?? "OrderPlaced",
                doc["customer"]?.ToString() ?? string.Empty,
                doc["product"]?.ToString() ?? string.Empty,
                DateTimeOffset.UtcNow);

            Deliver(delivered, viaOutbox: true);
        }

        return Task.CompletedTask;
    }

    private void Deliver(DeliveredEvent ev, bool viaOutbox)
    {
        // At-least-once delivery: dedupe by event id so a redelivery is a no-op.
        if (_downstream.TryAdd(ev.Id, ev))
        {
            if (viaOutbox)
            {
                Log($"Change feed relayed the {ev.EventType} event for {ev.OrderId} to downstream (shipping + email notified).", LogLevel.Success);
            }
            Changed?.Invoke();
        }
    }

    public IReadOnlyList<LogEntry> Timeline()
    {
        lock (_logSync)
        {
            return _log.ToList();
        }
    }

    public IReadOnlyList<DeliveredEvent> Downstream() =>
        _downstream.Values.OrderByDescending(d => d.DeliveredAt).ToList();

    public async Task<List<Order>> GetOrdersAsync()
    {
        var results = new List<Order>();
        if (_container is null) return results;
        using FeedIterator<Order> feed = _container.GetItemQueryIterator<Order>(
            "SELECT TOP 15 * FROM c WHERE c.docType = 'order' ORDER BY c._ts DESC");
        while (feed.HasMoreResults)
        {
            results.AddRange(await feed.ReadNextAsync());
        }
        return results;
    }

    public async Task<List<OutboxEvent>> GetOutboxAsync()
    {
        var results = new List<OutboxEvent>();
        if (_container is null) return results;
        using FeedIterator<OutboxEvent> feed = _container.GetItemQueryIterator<OutboxEvent>(
            "SELECT TOP 15 * FROM c WHERE c.docType = 'event' ORDER BY c._ts DESC");
        while (feed.HasMoreResults)
        {
            results.AddRange(await feed.ReadNextAsync());
        }
        return results;
    }

    /// <summary>True once the change feed has relayed this outbox event to the downstream consumer.</summary>
    public bool IsDelivered(string eventId) => _downstream.ContainsKey(eventId);

    /// <summary>Clears all orders, events, and counters so the demo can be run again cleanly.</summary>
    public async Task ResetAsync()
    {
        if (_container is null) return;

        var ids = new List<(string Id, string Pk)>();
        using (FeedIterator<JObject> feed = _container.GetItemQueryIterator<JObject>("SELECT c.id, c.orderId FROM c"))
        {
            while (feed.HasMoreResults)
            {
                foreach (JObject doc in await feed.ReadNextAsync())
                {
                    ids.Add((doc["id"]?.ToString() ?? string.Empty, doc["orderId"]?.ToString() ?? string.Empty));
                }
            }
        }

        foreach ((string id, string pk) in ids)
        {
            try { await _container.DeleteItemAsync<JObject>(id, new PartitionKey(pk)); }
            catch (CosmosException) { /* already gone */ }
        }

        _downstream.Clear();
        Interlocked.Exchange(ref _ordersPlaced, 0);
        Interlocked.Exchange(ref _eventsLost, 0);
        lock (_logSync) { _log.Clear(); }
        Log("Reset — cleared all orders, events, and counters.", LogLevel.Info);
        Changed?.Invoke();
    }

    private void Log(string message, LogLevel level)
    {
        lock (_logSync)
        {
            _log.AddFirst(new LogEntry(DateTimeOffset.UtcNow, message, level));
            while (_log.Count > 60) _log.RemoveLast();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.StopAsync();
        _client?.Dispose();
    }
}
