using System.Net;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.PatchApi;

// ---------------------------------------------------------------------------
// Model – an order whose fields are owned by different services. The Patch API
// lets each service update only its own field.
// ---------------------------------------------------------------------------

public class Order
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("paymentStatus")] public string PaymentStatus { get; set; } = "Pending";
    [JsonProperty("shippingStatus")] public string ShippingStatus { get; set; } = "NotShipped";
    [JsonProperty("viewCount")] public int ViewCount { get; set; }
    [JsonProperty("tags")] public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Integration tests for the Patch API (partial document update) design pattern.
///
/// These verify the behaviours the sample relies on:
///   - a Patch <c>Set</c> updates one field and leaves the others untouched;
///   - <c>Increment</c> and array <c>Add</c> operations work as documented;
///   - Patch costs fewer RUs than a read-modify-write (it skips the read);
///   - two callers patching DIFFERENT fields both survive, whereas a
///     read-modify-write + full replace loses one of the two updates.
/// </summary>
public class PatchApiTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"PatchApiTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public PatchApiTests(EmulatorFixture fixture)
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

    private async Task<Order> SeedAsync()
    {
        string orderId = $"ORD-{Guid.NewGuid():N}";
        var order = new Order { Id = orderId, OrderId = orderId };
        await _container.CreateItemAsync(order, new PartitionKey(orderId));
        return order;
    }

    private Task<ItemResponse<Order>> ReadAsync(string orderId) =>
        _container.ReadItemAsync<Order>(orderId, new PartitionKey(orderId));

    [Fact]
    public async Task Patch_Set_UpdatesOnlyTheTargetedField()
    {
        Order order = await SeedAsync();

        await _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
            new[] { PatchOperation.Set("/paymentStatus", "Paid") });

        Order updated = (await ReadAsync(order.OrderId)).Resource;
        Assert.Equal("Paid", updated.PaymentStatus);
        // The field we did NOT patch is unchanged.
        Assert.Equal("NotShipped", updated.ShippingStatus);
    }

    [Fact]
    public async Task Patch_Increment_And_Add_Work()
    {
        Order order = await SeedAsync();

        await _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
            new[] { PatchOperation.Increment("/viewCount", 3) });
        await _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
            new[] { PatchOperation.Add("/tags/-", "gift") });

        Order updated = (await ReadAsync(order.OrderId)).Resource;
        Assert.Equal(3, updated.ViewCount);
        Assert.Contains("gift", updated.Tags);
    }

    [Fact]
    public async Task Patch_CostsFewerRu_ThanReadModifyWrite()
    {
        Order order = await SeedAsync();

        // Patch: one operation, no read.
        double patchRu = (await _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
            new[] { PatchOperation.Set("/paymentStatus", "Paid") })).RequestCharge;

        // Read-modify-write: read the whole document, then replace it.
        ItemResponse<Order> read = await ReadAsync(order.OrderId);
        double readRu = read.RequestCharge;
        Order doc = read.Resource;
        doc.PaymentStatus = "Refunded";
        double replaceRu = (await _container.ReplaceItemAsync(doc, order.Id, new PartitionKey(order.OrderId))).RequestCharge;

        // Patch skips the read, so it must cost less than read + replace combined.
        Assert.True(patchRu < readRu + replaceRu,
            $"Expected patch ({patchRu} RU) < read ({readRu}) + replace ({replaceRu}) RU.");
    }

    [Fact]
    public async Task Patch_ConcurrentDifferentFields_PreservesBoth()
    {
        Order order = await SeedAsync();

        // Two "services" patch different fields concurrently — neither reads the document.
        await Task.WhenAll(
            _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
                new[] { PatchOperation.Set("/paymentStatus", "Paid") }),
            _container.PatchItemAsync<Order>(order.Id, new PartitionKey(order.OrderId),
                new[] { PatchOperation.Set("/shippingStatus", "Shipped") }));

        Order updated = (await ReadAsync(order.OrderId)).Resource;
        Assert.Equal("Paid", updated.PaymentStatus);
        Assert.Equal("Shipped", updated.ShippingStatus);
    }

    [Fact]
    public async Task ReadModifyWrite_ConcurrentDifferentFields_LosesOne()
    {
        Order order = await SeedAsync();

        // Both services read the SAME version of the document...
        Order paymentView = (await ReadAsync(order.OrderId)).Resource;
        Order shippingView = (await ReadAsync(order.OrderId)).Resource;

        // ...each changes only its own field...
        paymentView.PaymentStatus = "Paid";
        shippingView.ShippingStatus = "Shipped";

        // ...and each replaces the WHOLE document. The second write clobbers the first field.
        await _container.ReplaceItemAsync(paymentView, order.Id, new PartitionKey(order.OrderId));
        await _container.ReplaceItemAsync(shippingView, order.Id, new PartitionKey(order.OrderId));

        Order updated = (await ReadAsync(order.OrderId)).Resource;
        // Shipping wrote last from a stale read, so payment's update was LOST.
        Assert.Equal("Shipped", updated.ShippingStatus);
        Assert.Equal("Pending", updated.PaymentStatus); // "Paid" was overwritten
    }

    [Fact]
    public async Task ReadModifyWriteWithETag_ConcurrentDifferentFields_Conflicts_EvenThoughFieldsDiffer()
    {
        Order order = await SeedAsync();
        var pk = new PartitionKey(order.OrderId);

        // Both services read the SAME version — capturing the same ETag.
        ItemResponse<Order> paymentRead = await ReadAsync(order.OrderId);
        ItemResponse<Order> shippingRead = await ReadAsync(order.OrderId);

        // Payment writes first with its ETag — still matches, so it succeeds.
        Order paymentView = paymentRead.Resource;
        paymentView.PaymentStatus = "Paid";
        await _container.ReplaceItemAsync(paymentView, order.Id, pk,
            new ItemRequestOptions { IfMatchEtag = paymentRead.ETag });

        // Shipping changed a DIFFERENT field, but its ETag is now stale (the ETag guards the whole
        // document), so its IfMatchEtag replace must fail with 412 Precondition Failed.
        Order shippingView = shippingRead.Resource;
        shippingView.ShippingStatus = "Shipped";
        bool conflicted = false;
        try
        {
            await _container.ReplaceItemAsync(shippingView, order.Id, pk,
                new ItemRequestOptions { IfMatchEtag = shippingRead.ETag });
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            conflicted = true;
            // Retry: re-read to get the current ETag, re-apply, and replace again.
            ItemResponse<Order> retry = await ReadAsync(order.OrderId);
            Order retryView = retry.Resource;
            retryView.ShippingStatus = "Shipped";
            await _container.ReplaceItemAsync(retryView, order.Id, pk,
                new ItemRequestOptions { IfMatchEtag = retry.ETag });
        }

        // The needless conflict is the point: different fields still collide under whole-document ETags.
        Assert.True(conflicted, "Expected a 412 conflict even though the two writers changed different fields.");

        // After the retry the result is correct — both updates survive — but it cost the extra work.
        Order final = (await ReadAsync(order.OrderId)).Resource;
        Assert.Equal("Paid", final.PaymentStatus);
        Assert.Equal("Shipped", final.ShippingStatus);
    }
}
