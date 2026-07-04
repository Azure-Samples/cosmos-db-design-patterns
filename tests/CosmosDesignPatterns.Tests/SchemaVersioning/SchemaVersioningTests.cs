using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.SchemaVersioning;

// ---------------------------------------------------------------------------
// Models – mirror both schema versions used by the schema-versioning pattern
// ---------------------------------------------------------------------------

/// <summary>Version 1 cart – no SchemaVersion field.</summary>
public class CartV1
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public long CustomerId { get; set; }
    public List<CartItemV1>? Items { get; set; }
}

public class CartItemV1
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Version 2 cart – adds SchemaVersion and IsSpecialOrder support.</summary>
public class CartV2
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public long CustomerId { get; set; }
    public List<CartItemV2>? Items { get; set; }
    public int SchemaVersion { get; set; } = 2;
}

public class CartItemV2
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsSpecialOrder { get; set; }
    public string? SpecialOrderNotes { get; set; }
}

/// <summary>
/// Flexible read model that can deserialise both V1 and V2 carts.
/// When SchemaVersion is absent (V1) the property defaults to null.
/// </summary>
public class CartReadModel
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int? SchemaVersion { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Schema Versioning design pattern.
///
/// The pattern stores multiple schema versions of the same entity type in one
/// container.  A <c>schemaVersion</c> field (absent in V1, present in V2)
/// allows the application to apply the correct deserialization path.
///
/// These tests verify:
///   - V1 carts (no SchemaVersion) round-trip correctly.
///   - V2 carts (SchemaVersion = 2) round-trip correctly.
///   - Both schema versions coexist in the same container.
///   - A query can distinguish versions by the presence of SchemaVersion.
/// </summary>
[Collection("Emulator")]
public class SchemaVersioningTests : IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"SchemaVersionTest-{Guid.NewGuid():N}";
    private Container _cartsContainer = default!;

    public SchemaVersioningTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Partition key matches the schema-versioning appsettings (/id).
        _cartsContainer = await db.CreateContainerIfNotExistsAsync("ShoppingCart", "/id");
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_V1Cart_WithoutSchemaVersion_Succeeds()
    {
        var cart = new CartV1
        {
            CustomerId = 1,
            Items = [new CartItemV1 { ProductName = "Widget", Quantity = 3 }]
        };

        await _cartsContainer.UpsertItemAsync(cart, new PartitionKey(cart.Id));

        var result = await _cartsContainer.ReadItemAsync<CartV1>(cart.Id, new PartitionKey(cart.Id));

        Assert.Equal(cart.Id, result.Resource.Id);
        Assert.Equal(cart.CustomerId, result.Resource.CustomerId);
        Assert.Single(result.Resource.Items!);
        Assert.Equal("Widget", result.Resource.Items![0].ProductName);
    }

    [Fact]
    public async Task SaveAndRetrieve_V2Cart_WithSchemaVersion_Succeeds()
    {
        var cart = new CartV2
        {
            CustomerId = 2,
            Items =
            [
                new CartItemV2 { ProductName = "Gadget", Quantity = 1, IsSpecialOrder = true,
                    SpecialOrderNotes = "Gift wrap" }
            ]
        };

        await _cartsContainer.UpsertItemAsync(cart, new PartitionKey(cart.Id));

        var result = await _cartsContainer.ReadItemAsync<CartV2>(cart.Id, new PartitionKey(cart.Id));

        Assert.Equal(2, result.Resource.SchemaVersion);
        Assert.True(result.Resource.Items![0].IsSpecialOrder);
        Assert.Equal("Gift wrap", result.Resource.Items[0].SpecialOrderNotes);
    }

    [Fact]
    public async Task BothSchemaVersions_Coexist_InSameContainer()
    {
        var v1 = new CartV1 { CustomerId = 10 };
        var v2 = new CartV2 { CustomerId = 20 };

        await _cartsContainer.UpsertItemAsync(v1, new PartitionKey(v1.Id));
        await _cartsContainer.UpsertItemAsync(v2, new PartitionKey(v2.Id));

        // Read both back as flexible model
        var r1 = await _cartsContainer.ReadItemAsync<CartReadModel>(v1.Id, new PartitionKey(v1.Id));
        var r2 = await _cartsContainer.ReadItemAsync<CartReadModel>(v2.Id, new PartitionKey(v2.Id));

        // V1 has no SchemaVersion field → deserialises as null
        Assert.Null(r1.Resource.SchemaVersion);
        // V2 has SchemaVersion = 2
        Assert.Equal(2, r2.Resource.SchemaVersion);
    }

    [Fact]
    public async Task Query_CanDistinguish_V2Carts_BySchemaVersion()
    {
        // Insert one V1 and two V2 carts
        var v1 = new CartV1 { CustomerId = 30 };
        await _cartsContainer.UpsertItemAsync(v1, new PartitionKey(v1.Id));

        var v2a = new CartV2 { CustomerId = 31 };
        var v2b = new CartV2 { CustomerId = 32 };
        await _cartsContainer.UpsertItemAsync(v2a, new PartitionKey(v2a.Id));
        await _cartsContainer.UpsertItemAsync(v2b, new PartitionKey(v2b.Id));

        // Query for V2 carts only (where SchemaVersion is defined)
        var v2Carts = new List<CartReadModel>();
        using FeedIterator<CartReadModel> feed = _cartsContainer
            .GetItemQueryIterator<CartReadModel>(
                "SELECT * FROM c WHERE IS_DEFINED(c.SchemaVersion) AND c.SchemaVersion = 2");

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            v2Carts.AddRange(page.Resource);
        }

        Assert.True(v2Carts.Count >= 2,
            $"Expected at least 2 V2 carts but found {v2Carts.Count}.");
        Assert.All(v2Carts, c => Assert.Equal(2, c.SchemaVersion));
    }

    [Fact]
    public async Task Query_V2Cart_WithSpecialOrderItem_HasSpecialOrders()
    {
        var cart = new CartV2
        {
            CustomerId = 99,
            Items =
            [
                new CartItemV2 { ProductName = "Normal Item", Quantity = 1, IsSpecialOrder = false },
                new CartItemV2 { ProductName = "Special Item", Quantity = 1, IsSpecialOrder = true,
                    SpecialOrderNotes = "Rush order" }
            ]
        };

        await _cartsContainer.UpsertItemAsync(cart, new PartitionKey(cart.Id));

        var result = await _cartsContainer.ReadItemAsync<CartV2>(cart.Id, new PartitionKey(cart.Id));

        Assert.NotNull(result.Resource.Items);
        bool hasSpecialOrders = result.Resource.Items!.Any(i => i.IsSpecialOrder);
        Assert.True(hasSpecialOrders, "Cart should contain at least one special-order item.");
    }
}
