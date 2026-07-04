using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.AttributeArray;

// ---------------------------------------------------------------------------
// Models – mirror the attribute-array pattern source models
// ---------------------------------------------------------------------------

/// <summary>Product where each size is stored as a separate top-level property.</summary>
public class AttributePropertyProduct
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Price { get; set; } = "0.00";
    public int SizeSmall { get; set; }
    public int SizeMedium { get; set; }
    public int SizeLarge { get; set; }
    public string EntityType { get; set; } = "Attribute Properties";
}

/// <summary>Product where sizes are stored in an array (the preferred pattern).</summary>
public class AttributeArrayProduct
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Price { get; set; } = "0.00";
    public List<ProductSize> Sizes { get; set; } = [];
    public string EntityType { get; set; } = "Attribute Array";
}

public class ProductSize
{
    public string Size { get; set; } = string.Empty;
    public int Count { get; set; }
}

// Query result shapes
public class PropertyQueryResult
{
    public string Name { get; set; } = string.Empty;
    public int SizeSmall { get; set; }
    public int SizeMedium { get; set; }
    public int SizeLarge { get; set; }
}

public class ArrayQueryResult
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Attribute Array design pattern.
///
/// The pattern compares storing variable attributes as individual document
/// properties versus storing them in an array.  The array approach allows a
/// single, simple JOIN query instead of multiple OR clauses.
///
/// These tests verify:
///   - Both product representations can be stored and retrieved.
///   - The property-based query (multiple OR clauses) returns matching items.
///   - The array-based query (JOIN) returns matching items.
///   - Both queries agree on which products meet the threshold.
/// </summary>
[Collection("Emulator")]
public class AttributeArrayTests : IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"AttributeArrayTest-{Guid.NewGuid():N}";
    private Container _productsContainer = default!;

    public AttributeArrayTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Partition key matches the attribute-array pattern (/productId).
        _productsContainer = await db.CreateContainerIfNotExistsAsync("products", "/productId");
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Creates a pair of equivalent products (one per pattern) sharing a productId.</summary>
    private static (AttributePropertyProduct prop, AttributeArrayProduct arr) CreateProductPair(
        string name, int small, int medium, int large)
    {
        string productId = Guid.NewGuid().ToString();

        var prop = new AttributePropertyProduct
        {
            ProductId = productId,
            Name = name,
            Category = "Test",
            Price = "9.99",
            SizeSmall = small,
            SizeMedium = medium,
            SizeLarge = large
        };

        var arr = new AttributeArrayProduct
        {
            ProductId = productId,
            Name = name,
            Category = "Test",
            Price = "9.99",
            Sizes =
            [
                new ProductSize { Size = "Small",  Count = small  },
                new ProductSize { Size = "Medium", Count = medium },
                new ProductSize { Size = "Large",  Count = large  }
            ]
        };

        return (prop, arr);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_AttributePropertyProduct_Succeeds()
    {
        var (prop, _) = CreateProductPair("Widget", small: 10, medium: 20, large: 30);

        await _productsContainer.UpsertItemAsync(prop, new PartitionKey(prop.ProductId));

        var result = await _productsContainer.ReadItemAsync<AttributePropertyProduct>(
            prop.Id, new PartitionKey(prop.ProductId));

        Assert.Equal(prop.Name, result.Resource.Name);
        Assert.Equal(10, result.Resource.SizeSmall);
        Assert.Equal(20, result.Resource.SizeMedium);
        Assert.Equal(30, result.Resource.SizeLarge);
    }

    [Fact]
    public async Task SaveAndRetrieve_AttributeArrayProduct_Succeeds()
    {
        var (_, arr) = CreateProductPair("Gadget", small: 5, medium: 15, large: 25);

        await _productsContainer.UpsertItemAsync(arr, new PartitionKey(arr.ProductId));

        var result = await _productsContainer.ReadItemAsync<AttributeArrayProduct>(
            arr.Id, new PartitionKey(arr.ProductId));

        Assert.Equal(arr.Name, result.Resource.Name);
        Assert.Equal(3, result.Resource.Sizes.Count);

        var small = result.Resource.Sizes.Single(s => s.Size == "Small");
        Assert.Equal(5, small.Count);
    }

    [Fact]
    public async Task BothProductTypes_Coexist_InSameContainer()
    {
        var (prop, arr) = CreateProductPair("Coexist Product", small: 1, medium: 2, large: 3);

        // Both share the same productId partition but differ by document id and entityType.
        await _productsContainer.UpsertItemAsync(prop, new PartitionKey(prop.ProductId));
        await _productsContainer.UpsertItemAsync(arr, new PartitionKey(arr.ProductId));

        var propResult = await _productsContainer.ReadItemAsync<AttributePropertyProduct>(
            prop.Id, new PartitionKey(prop.ProductId));
        var arrResult = await _productsContainer.ReadItemAsync<AttributeArrayProduct>(
            arr.Id, new PartitionKey(arr.ProductId));

        Assert.Equal("Attribute Properties", propResult.Resource.EntityType);
        Assert.Equal("Attribute Array", arrResult.Resource.EntityType);
    }

    [Fact]
    public async Task PropertyBasedQuery_WithMultipleOrClauses_ReturnsMatchingProducts()
    {
        // Insert products: two above threshold (80), one below.
        const int threshold = 80;
        var (above1, _) = CreateProductPair("Above 1", small: 90, medium: 50, large: 30);
        var (above2, _) = CreateProductPair("Above 2", small: 10, medium: 85, large: 20);
        var (below, _)  = CreateProductPair("Below",   small: 10, medium: 20, large: 30);

        await _productsContainer.UpsertItemAsync(above1, new PartitionKey(above1.ProductId));
        await _productsContainer.UpsertItemAsync(above2, new PartitionKey(above2.ProductId));
        await _productsContainer.UpsertItemAsync(below, new PartitionKey(below.ProductId));

        // Property-based query: mirrors the sample query in the pattern.
        string sql = @"
            SELECT p.name, p.sizeSmall, p.sizeMedium, p.sizeLarge
            FROM products p
            WHERE p.entityType = 'Attribute Properties'
              AND (p.sizeSmall >= @qty OR p.sizeMedium >= @qty OR p.sizeLarge >= @qty)";

        var query = new QueryDefinition(sql).WithParameter("@qty", threshold);
        var results = new List<PropertyQueryResult>();

        using FeedIterator<PropertyQueryResult> feed =
            _productsContainer.GetItemQueryIterator<PropertyQueryResult>(query);

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        Assert.True(results.Count >= 2,
            $"Expected ≥ 2 matching property-based products, found {results.Count}.");
        Assert.DoesNotContain(results, r => r.Name == "Below");
    }

    [Fact]
    public async Task ArrayBasedQuery_WithJoin_ReturnsMatchingProductSizes()
    {
        // Insert array-based products: one with a large size above threshold.
        const int threshold = 80;
        var (_, arrAbove) = CreateProductPair("Array Above", small: 90, medium: 50, large: 30);
        var (_, arrBelow) = CreateProductPair("Array Below", small: 10, medium: 20, large: 30);

        await _productsContainer.UpsertItemAsync(arrAbove, new PartitionKey(arrAbove.ProductId));
        await _productsContainer.UpsertItemAsync(arrBelow, new PartitionKey(arrBelow.ProductId));

        // Array-based query: mirrors the sample query in the pattern.
        string sql = @"
            SELECT p.name, s.size, s.count
            FROM products p
            JOIN s IN p.sizes
            WHERE p.entityType = 'Attribute Array'
              AND s.count >= @qty";

        var query = new QueryDefinition(sql).WithParameter("@qty", threshold);
        var results = new List<ArrayQueryResult>();

        using FeedIterator<ArrayQueryResult> feed =
            _productsContainer.GetItemQueryIterator<ArrayQueryResult>(query);

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        Assert.True(results.Count >= 1,
            $"Expected ≥ 1 matching array-based product size, found {results.Count}.");

        // Every returned row must have count ≥ threshold.
        Assert.All(results, r => Assert.True(r.Count >= threshold,
            $"Returned row '{r.Name}/{r.Size}' has count {r.Count} below threshold {threshold}."));
    }
}
