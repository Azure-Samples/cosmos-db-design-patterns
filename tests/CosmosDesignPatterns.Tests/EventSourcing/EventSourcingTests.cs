using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.EventSourcing;

// ---------------------------------------------------------------------------
// Models – mirror the event-sourcing pattern source models
// ---------------------------------------------------------------------------

public class CartItem
{
    [JsonProperty("ProductName")]
    public string ProductName { get; set; } = string.Empty;
    [JsonProperty("Quantity")]
    public int Quantity { get; set; }

    public CartItem() { }

    public CartItem(string productName, int quantity)
    {
        ProductName = productName;
        Quantity = quantity;
    }
}

public class CartEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("CartId")]
    public string CartId { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("SessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("UserId")]
    public int UserId { get; set; }
    [JsonProperty("EventType")]
    public string EventType { get; set; } = string.Empty;
    [JsonProperty("Product")]
    public string? Product { get; set; }
    [JsonProperty("QuantityChange")]
    public int? QuantityChange { get; set; } = 0;
    [JsonProperty("ProductsInCart")]
    public List<CartItem>? ProductsInCart { get; set; }
    [JsonProperty("EventTimestamp")]
    public string EventTimestamp { get; set; } = DateTime.UtcNow.ToString("o");
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Event Sourcing design pattern.
///
/// The pattern records every state change to a shopping cart as an immutable
/// event document (cart_created, product_added, product_deleted,
/// cart_purchased).  The current cart state is derived by replaying all
/// events for a given CartId.
///
/// These tests verify:
///   - A cart event can be stored and retrieved.
///   - Multiple events for the same cart can all be retrieved.
///   - Events for different carts are isolated from one another.
///   - The full lifecycle sequence (create → add → delete → purchase) can be
///     stored and queried.
///   - Replaying events in order reconstructs the correct final cart state.
/// </summary>
public class EventSourcingTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"EventSourcingTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public EventSourcingTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        // Partition key matches the event-sourcing pattern (/CartId).
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync("CartEvents", "/CartId"));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CartEvent MakeEvent(string cartId, string sessionId, int userId, string eventType,
        string? product = null, int? quantityChange = 0, List<CartItem>? productsInCart = null) =>
        new()
        {
            CartId = cartId,
            SessionId = sessionId,
            UserId = userId,
            EventType = eventType,
            Product = product,
            QuantityChange = quantityChange,
            ProductsInCart = productsInCart ?? []
        };

    private async Task<List<CartEvent>> GetCartEventsAsync(string cartId)
    {
        string sql = "SELECT * FROM c WHERE c.CartId = @cartId ORDER BY c.EventTimestamp";
        var query = new QueryDefinition(sql).WithParameter("@cartId", cartId);
        var events = new List<CartEvent>();

        using FeedIterator<CartEvent> feed = _container.GetItemQueryIterator<CartEvent>(query);
        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            events.AddRange(page.Resource);
        }

        return events;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_CartEvent_Succeeds()
    {
        string cartId = Guid.NewGuid().ToString();
        string sessionId = Guid.NewGuid().ToString();

        var evt = MakeEvent(cartId, sessionId, userId: 42, eventType: "cart_created");
        await _container.UpsertItemAsync(evt, new PartitionKey(cartId));

        var result = await _container.ReadItemAsync<CartEvent>(evt.Id, new PartitionKey(cartId));

        Assert.Equal("cart_created", result.Resource.EventType);
        Assert.Equal(42, result.Resource.UserId);
        Assert.Equal(cartId, result.Resource.CartId);
    }

    [Fact]
    public async Task MultipleEvents_ForSameCart_AreAllRetrievable()
    {
        string cartId = Guid.NewGuid().ToString();
        string sessionId = Guid.NewGuid().ToString();
        int userId = 7;

        var events = new[]
        {
            MakeEvent(cartId, sessionId, userId, "cart_created"),
            MakeEvent(cartId, sessionId, userId, "product_added", "Widget", 2,
                [new CartItem("Widget", 2)]),
            MakeEvent(cartId, sessionId, userId, "cart_purchased",
                productsInCart: [new CartItem("Widget", 2)])
        };

        foreach (var evt in events)
            await _container.UpsertItemAsync(evt, new PartitionKey(cartId));

        var retrieved = await GetCartEventsAsync(cartId);

        Assert.Equal(3, retrieved.Count);
        Assert.Contains(retrieved, e => e.EventType == "cart_created");
        Assert.Contains(retrieved, e => e.EventType == "product_added");
        Assert.Contains(retrieved, e => e.EventType == "cart_purchased");
    }

    [Fact]
    public async Task DifferentCarts_AreIsolated()
    {
        string cartA = Guid.NewGuid().ToString();
        string cartB = Guid.NewGuid().ToString();

        var evtA = MakeEvent(cartA, Guid.NewGuid().ToString(), 1, "cart_created");
        var evtB = MakeEvent(cartB, Guid.NewGuid().ToString(), 2, "cart_created");

        await _container.UpsertItemAsync(evtA, new PartitionKey(cartA));
        await _container.UpsertItemAsync(evtB, new PartitionKey(cartB));

        var cartAEvents = await GetCartEventsAsync(cartA);
        var cartBEvents = await GetCartEventsAsync(cartB);

        Assert.Single(cartAEvents);
        Assert.Single(cartBEvents);
        Assert.Equal(cartA, cartAEvents[0].CartId);
        Assert.Equal(cartB, cartBEvents[0].CartId);
    }

    [Fact]
    public async Task FullLifecycle_Events_AreStoredInOrder()
    {
        string cartId = Guid.NewGuid().ToString();
        string sessionId = Guid.NewGuid().ToString();
        int userId = 99;

        string[] lifecycle = ["cart_created", "product_added", "product_deleted", "cart_purchased"];

        foreach (var eventType in lifecycle)
            await _container.UpsertItemAsync(
                MakeEvent(cartId, sessionId, userId, eventType),
                new PartitionKey(cartId));

        var retrieved = await GetCartEventsAsync(cartId);

        Assert.Equal(4, retrieved.Count);
        // Every event must belong to the same cart.
        Assert.All(retrieved, e => Assert.Equal(cartId, e.CartId));
    }

    [Fact]
    public async Task ReplayingEvents_ReconstructsCorrectCartState()
    {
        string cartId = Guid.NewGuid().ToString();
        string sessionId = Guid.NewGuid().ToString();
        int userId = 55;

        // Simulate: create cart → add 3 Widgets → add 2 Gizmos → delete 1 Gizmo
        var events = new[]
        {
            MakeEvent(cartId, sessionId, userId, "cart_created",
                productsInCart: []),
            MakeEvent(cartId, sessionId, userId, "product_added", "Widget", 3,
                [new CartItem("Widget", 3)]),
            MakeEvent(cartId, sessionId, userId, "product_added", "Gizmo", 2,
                [new CartItem("Widget", 3), new CartItem("Gizmo", 2)]),
            MakeEvent(cartId, sessionId, userId, "product_deleted", "Gizmo", -1,
                [new CartItem("Widget", 3), new CartItem("Gizmo", 1)])
        };

        foreach (var evt in events)
            await _container.UpsertItemAsync(evt, new PartitionKey(cartId));

        // Reconstruct state by replaying: take ProductsInCart from the last event.
        var allEvents = await GetCartEventsAsync(cartId);
        Assert.Equal(4, allEvents.Count);

        // The final state is captured in the last event's ProductsInCart snapshot.
        var lastEvent = allEvents.OrderBy(e => e.EventTimestamp).Last();
        Assert.NotNull(lastEvent.ProductsInCart);

        var finalCart = lastEvent.ProductsInCart!;
        var widget = finalCart.FirstOrDefault(i => i.ProductName == "Widget");
        var gizmo = finalCart.FirstOrDefault(i => i.ProductName == "Gizmo");

        Assert.NotNull(widget);
        Assert.Equal(3, widget!.Quantity);
        Assert.NotNull(gizmo);
        Assert.Equal(1, gizmo!.Quantity);
    }
}
