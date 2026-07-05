using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.DocumentVersioning;

// ---------------------------------------------------------------------------
// Models – mirror the document-versioning pattern source models
// ---------------------------------------------------------------------------

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public int CustomerId { get; set; }
    public string Status { get; set; } = "Submitted";
    public List<OrderItem>? OrderDetails { get; set; }
}

public class VersionedOrder : Order
{
    public int DocumentVersion { get; set; } = 1;

    public VersionedOrder() { }

    public VersionedOrder(Order source)
    {
        Id = source.Id;
        OrderId = source.OrderId;
        OrderDate = source.OrderDate;
        CustomerId = source.CustomerId;
        Status = source.Status;
        OrderDetails = source.OrderDetails;
    }
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double Price { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Document Versioning design pattern.
///
/// The pattern stores each new status of an order as an incremented
/// DocumentVersion on the same document.  A separate history container
/// (populated via Change Feed) retains all prior versions.
///
/// These tests verify:
///   - An order can be saved to and retrieved from Cosmos DB.
///   - Each status transition increments DocumentVersion.
///   - Multiple sequential transitions produce the correct final version.
///   - A cross-partition query returns all stored orders.
/// </summary>
public class DocumentVersioningTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    // Each test class instance gets a unique database name to prevent
    // cross-test interference when xUnit runs tests in parallel.
    private readonly string _databaseName = $"DocVersionTest-{Guid.NewGuid():N}";
    private Container _ordersContainer = default!;

    public DocumentVersioningTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database database = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        // Partition key matches the document-versioning pattern (/CustomerId).
        _ordersContainer = await EmulatorFixture.WithRetryAsync(() => database.CreateContainerIfNotExistsAsync("Orders", "/CustomerId"));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Order CreateTestOrder(int customerId = 1) => new()
    {
        OrderId = Guid.NewGuid().ToString(),
        CustomerId = customerId,
        Status = "Submitted",
        OrderDetails =
        [
            new OrderItem { ProductName = "Widget", Quantity = 2, Price = 9.99 }
        ]
    };

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveOrder_And_RetrieveById_Succeeds()
    {
        var order = CreateTestOrder();

        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var result = await _ordersContainer.ReadItemAsync<Order>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal(order.OrderId, result.Resource.OrderId);
        Assert.Equal("Submitted", result.Resource.Status);
        Assert.Equal(order.CustomerId, result.Resource.CustomerId);
    }

    [Fact]
    public async Task NewVersionedOrder_HasDocumentVersion_One()
    {
        var order = new VersionedOrder(CreateTestOrder());

        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var result = await _ordersContainer.ReadItemAsync<VersionedOrder>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal(1, result.Resource.DocumentVersion);
    }

    [Fact]
    public async Task CancelOrder_SetsStatus_And_IncrementsDocumentVersion()
    {
        var order = new VersionedOrder(CreateTestOrder());
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        // Simulate the OrderHelper.CancelOrder + HandleVersioning logic.
        order.Status = "Cancelled";
        order.DocumentVersion++;
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var result = await _ordersContainer.ReadItemAsync<VersionedOrder>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal("Cancelled", result.Resource.Status);
        Assert.Equal(2, result.Resource.DocumentVersion);
    }

    [Fact]
    public async Task FulfillOrder_SetsStatus_And_IncrementsDocumentVersion()
    {
        var order = new VersionedOrder(CreateTestOrder());
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        order.Status = "Fulfilled";
        order.DocumentVersion++;
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var result = await _ordersContainer.ReadItemAsync<VersionedOrder>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal("Fulfilled", result.Resource.Status);
        Assert.Equal(2, result.Resource.DocumentVersion);
    }

    [Fact]
    public async Task DeliverOrder_SetsStatus_And_IncrementsDocumentVersion()
    {
        var order = new VersionedOrder(CreateTestOrder());
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        order.Status = "Delivered";
        order.DocumentVersion++;
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var result = await _ordersContainer.ReadItemAsync<VersionedOrder>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal("Delivered", result.Resource.Status);
        Assert.Equal(2, result.Resource.DocumentVersion);
    }

    [Fact]
    public async Task FullLifecycle_VersionIncrements_Sequentially()
    {
        // Arrange – start at version 1 (Submitted)
        var order = new VersionedOrder(CreateTestOrder());
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        // Act – move through Fulfilled → Delivered (two transitions)
        order.Status = "Fulfilled";
        order.DocumentVersion++;
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        order.Status = "Delivered";
        order.DocumentVersion++;
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        // Assert
        var result = await _ordersContainer.ReadItemAsync<VersionedOrder>(
            order.Id, new PartitionKey(order.CustomerId));

        Assert.Equal("Delivered", result.Resource.Status);
        Assert.Equal(3, result.Resource.DocumentVersion);
    }

    [Fact]
    public async Task RetrieveAllOrders_Query_ReturnsInsertedOrders()
    {
        // Insert a few orders with unique customer IDs to make them identifiable.
        var ids = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var order = new VersionedOrder(CreateTestOrder(customerId: 100 + i));
            await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));
            ids.Add(order.OrderId);
        }

        // Cross-partition query (mirrors OrderHelper.RetrieveAllOrdersAsync).
        var retrieved = new List<VersionedOrder>();
        using FeedIterator<VersionedOrder> feed = _ordersContainer
            .GetItemQueryIterator<VersionedOrder>("SELECT * FROM Orders");

        while (feed.HasMoreResults)
        {
            FeedResponse<VersionedOrder> page = await feed.ReadNextAsync();
            retrieved.AddRange(page.Resource);
        }

        Assert.True(retrieved.Count >= 3,
            $"Expected at least 3 orders but found {retrieved.Count}.");

        foreach (string id in ids)
        {
            Assert.Contains(retrieved, o => o.OrderId == id);
        }
    }

    [Fact]
    public async Task RetrieveOrderByCustomerId_Query_ReturnsCorrectOrder()
    {
        var order = new VersionedOrder(CreateTestOrder(customerId: 999));
        await _ordersContainer.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        // Mirrors OrderHelper.RetrieveOrderAsync using LINQ on partition.
        IOrderedQueryable<VersionedOrder> queryable =
            _ordersContainer.GetItemLinqQueryable<VersionedOrder>();

        using FeedIterator<VersionedOrder> feed = queryable
            .Where(o => o.CustomerId == 999 && o.OrderId == order.OrderId)
            .ToFeedIterator();

        VersionedOrder? found = null;
        while (feed.HasMoreResults)
        {
            FeedResponse<VersionedOrder> page = await feed.ReadNextAsync();
            found = page.Resource.FirstOrDefault();
            if (found is not null) break;
        }

        Assert.NotNull(found);
        Assert.Equal(order.OrderId, found.OrderId);
    }
}
