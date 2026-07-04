using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.MaterializedView;

// ---------------------------------------------------------------------------
// Models – mirror the materialized-view pattern source models
// ---------------------------------------------------------------------------

/// <summary>Source sales document written by the data generator.</summary>
public class Sales
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("CustomerId")]
    public int CustomerId { get; set; }
    [JsonProperty("OrderId")]
    public int OrderId { get; set; }
    [JsonProperty("OrderDate")]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    [JsonProperty("Qty")]
    public int Qty { get; set; }
    [JsonProperty("Product")]
    public string Product { get; set; } = string.Empty;
    [JsonProperty("Total")]
    public double Total { get; set; }
}

/// <summary>
/// Materialized view document derived from <see cref="Sales"/>, partitioned
/// by <c>Product</c> for efficient product-centric queries.
/// </summary>
public class SalesByProduct
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("OrderDate")]
    public DateTime OrderDate { get; set; }
    [JsonProperty("Qty")]
    public int Qty { get; set; }
    [JsonProperty("Total")]
    public double Total { get; set; }
    [JsonProperty("Product")]
    public string Product { get; set; } = string.Empty;

    public SalesByProduct() { }

    public SalesByProduct(Sales source)
    {
        OrderDate = source.OrderDate;
        Product = source.Product;
        Qty = source.Qty;
        Total = source.Total;
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Materialized View design pattern.
///
/// The pattern maintains a pre-computed view container (<c>SalesByProduct</c>)
/// that is partitioned by <c>Product</c>.  The Azure Function
/// <c>MaterializedViewProcessor</c> populates it via Cosmos DB Change Feed
/// whenever a new document is written to the <c>Sales</c> container.
///
/// These tests verify the data layer behaviour that the function relies on:
///   - Sales records can be written to and read from the source container.
///   - A <c>SalesByProduct</c> view document can be derived from a Sales record.
///   - The view container can be queried efficiently by Product.
///   - Multiple Sales for the same product all appear in the view.
///   - Aggregating the view gives the correct total quantity and revenue.
/// </summary>
[Collection("Emulator")]
public class MaterializedViewTests : IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"MaterializedViewTest-{Guid.NewGuid():N}";
    private Container _salesContainer = default!;
    private Container _viewContainer = default!;

    public MaterializedViewTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Source container – partition by CustomerId (matches data-generator usage).
        _salesContainer = await db.CreateContainerIfNotExistsAsync("Sales", "/CustomerId");
        // Materialized view – partition by Product (matches function-app output binding).
        _viewContainer = await db.CreateContainerIfNotExistsAsync("SalesByProduct", "/Product");
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Sales MakeSale(string product, int qty, double unitPrice, int customerId = 10)
    {
        return new Sales
        {
            CustomerId = customerId,
            OrderId = Random.Shared.Next(1000, 9999),
            OrderDate = DateTime.UtcNow,
            Product = product,
            Qty = qty,
            Total = qty * unitPrice
        };
    }

    private async Task<List<SalesByProduct>> GetViewByProductAsync(string product)
    {
        string sql = "SELECT * FROM c WHERE c.Product = @product";
        var query = new QueryDefinition(sql).WithParameter("@product", product);
        var results = new List<SalesByProduct>();

        using FeedIterator<SalesByProduct> feed =
            _viewContainer.GetItemQueryIterator<SalesByProduct>(query);

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_SalesRecord_Succeeds()
    {
        var sale = MakeSale("Widget", qty: 5, unitPrice: 6.00);

        await _salesContainer.UpsertItemAsync(sale, new PartitionKey(sale.CustomerId));

        var result = await _salesContainer.ReadItemAsync<Sales>(
            sale.Id, new PartitionKey(sale.CustomerId));

        Assert.Equal("Widget", result.Resource.Product);
        Assert.Equal(5, result.Resource.Qty);
        Assert.Equal(30.0, result.Resource.Total, precision: 5);
    }

    [Fact]
    public async Task BuildMaterializedView_FromSalesRecord_Succeeds()
    {
        var sale = MakeSale("Gizmo", qty: 3, unitPrice: 5.00);

        await _salesContainer.UpsertItemAsync(sale, new PartitionKey(sale.CustomerId));

        // Simulate what MaterializedViewProcessor does: derive a view doc from the sale.
        var viewDoc = new SalesByProduct(sale);
        await _viewContainer.UpsertItemAsync(viewDoc, new PartitionKey(viewDoc.Product));

        var result = await _viewContainer.ReadItemAsync<SalesByProduct>(
            viewDoc.Id, new PartitionKey("Gizmo"));

        Assert.Equal("Gizmo", result.Resource.Product);
        Assert.Equal(3, result.Resource.Qty);
        Assert.Equal(15.0, result.Resource.Total, precision: 5);
    }

    [Fact]
    public async Task QueryByProduct_ReturnsOnlyMatchingSalesViewDocs()
    {
        var widgetSale = MakeSale("Widget", qty: 2, unitPrice: 6.00);
        var gizmoSale  = MakeSale("Gizmo",  qty: 4, unitPrice: 5.00);

        await _viewContainer.UpsertItemAsync(new SalesByProduct(widgetSale), new PartitionKey("Widget"));
        await _viewContainer.UpsertItemAsync(new SalesByProduct(gizmoSale),  new PartitionKey("Gizmo"));

        var widgetResults = await GetViewByProductAsync("Widget");
        var gizmoResults  = await GetViewByProductAsync("Gizmo");

        Assert.Single(widgetResults);
        Assert.Equal("Widget", widgetResults[0].Product);

        Assert.Single(gizmoResults);
        Assert.Equal("Gizmo", gizmoResults[0].Product);
    }

    [Fact]
    public async Task MultipleSalesForSameProduct_AllAppearInMaterializedView()
    {
        for (int i = 0; i < 3; i++)
        {
            var sale = MakeSale("Widget", qty: i + 1, unitPrice: 6.00, customerId: 10 + i);
            await _viewContainer.UpsertItemAsync(new SalesByProduct(sale), new PartitionKey("Widget"));
        }

        var results = await GetViewByProductAsync("Widget");

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal("Widget", r.Product));
    }

    [Fact]
    public async Task MaterializedView_AggregatesTotals_ByProduct()
    {
        // Insert 3 Widget sales with known quantities and prices.
        int[] quantities = [2, 3, 5];
        double unitPrice = 6.00;

        foreach (int qty in quantities)
        {
            var sale = MakeSale("Widget", qty, unitPrice, customerId: 20);
            await _viewContainer.UpsertItemAsync(new SalesByProduct(sale), new PartitionKey("Widget"));
        }

        var results = await GetViewByProductAsync("Widget");

        int totalQty = results.Sum(r => r.Qty);
        double totalRevenue = results.Sum(r => r.Total);

        Assert.Equal(quantities.Sum(), totalQty);
        Assert.Equal(quantities.Sum() * unitPrice, totalRevenue, precision: 5);
    }
}
