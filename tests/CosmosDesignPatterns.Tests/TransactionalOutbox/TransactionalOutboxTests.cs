using System.Net;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.TransactionalOutbox;

// ---------------------------------------------------------------------------
// Models – an order (business state) and an outbox event, co-located in one
// container by orderId and distinguished with a docType discriminator.
// ---------------------------------------------------------------------------

public class Order
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("docType")] public string DocType { get; set; } = "order";
    [JsonProperty("product")] public string Product { get; set; } = string.Empty;
}

public class OutboxEvent
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("docType")] public string DocType { get; set; } = "event";
    [JsonProperty("eventType")] public string EventType { get; set; } = "OrderPlaced";
}

/// <summary>
/// Integration tests for the Transactional Outbox design pattern.
///
/// The pattern writes the order and its outbox event into the same container in a single
/// <see cref="TransactionalBatch"/> (atomic within the order's partition), then a change feed
/// processor relays the events. These tests verify the guarantee the pattern relies on:
///   - a batch writes the order AND the event together (both present);
///   - if the batch fails, NEITHER is written (all-or-nothing — no dual-write anomaly);
///   - outbox events can be identified (by docType) for the relay to publish.
/// </summary>
public class TransactionalOutboxTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"OutboxTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public TransactionalOutboxTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync("OrderStore", "/orderId"));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    private async Task<bool> Exists(string id, string orderId)
    {
        try
        {
            await _container.ReadItemAsync<Order>(id, new PartitionKey(orderId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [Fact]
    public async Task Batch_WritesOrderAndEvent_Atomically()
    {
        string orderId = $"ORD-{Guid.NewGuid():N}";
        var order = new Order { Id = orderId, OrderId = orderId, Product = "Keyboard" };
        var evt = new OutboxEvent { Id = $"evt-{Guid.NewGuid():N}", OrderId = orderId };

        TransactionalBatchResponse response = await _container
            .CreateTransactionalBatch(new PartitionKey(orderId))
            .CreateItem(order)
            .CreateItem(evt)
            .ExecuteAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.True(await Exists(order.Id, orderId));
        Assert.True(await Exists(evt.Id, orderId));
    }

    [Fact]
    public async Task Batch_WhenItFails_WritesNeither()
    {
        string orderId = $"ORD-{Guid.NewGuid():N}";

        // Pre-create the order so a CreateItem for the same id will conflict (409) and fail the batch.
        var order = new Order { Id = orderId, OrderId = orderId, Product = "Mouse" };
        await _container.CreateItemAsync(order, new PartitionKey(orderId));

        var evt = new OutboxEvent { Id = $"evt-{Guid.NewGuid():N}", OrderId = orderId };

        TransactionalBatchResponse response = await _container
            .CreateTransactionalBatch(new PartitionKey(orderId))
            .CreateItem(order) // conflicts -> the whole batch is rejected
            .CreateItem(evt)
            .ExecuteAsync();

        Assert.False(response.IsSuccessStatusCode);
        // The event must NOT have been written, even though it was a valid operation on its own.
        Assert.False(await Exists(evt.Id, orderId));
    }

    [Fact]
    public async Task OutboxEvents_AreIdentifiableByDocType()
    {
        string orderId = $"ORD-{Guid.NewGuid():N}";
        await _container.CreateTransactionalBatch(new PartitionKey(orderId))
            .CreateItem(new Order { Id = orderId, OrderId = orderId, Product = "Monitor" })
            .CreateItem(new OutboxEvent { Id = $"evt-{Guid.NewGuid():N}", OrderId = orderId })
            .ExecuteAsync();

        int eventCount = 0;
        using FeedIterator<OutboxEvent> feed = _container.GetItemQueryIterator<OutboxEvent>(
            new QueryDefinition("SELECT * FROM c WHERE c.docType = 'event' AND c.orderId = @o").WithParameter("@o", orderId));
        while (feed.HasMoreResults)
        {
            eventCount += (await feed.ReadNextAsync()).Count;
        }

        Assert.Equal(1, eventCount);
    }
}
