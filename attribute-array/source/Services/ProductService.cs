using Bogus;
using AttributeArray.Models;
using Microsoft.Azure.Cosmos;
using Spectre.Console;
using Spectre.Console.Json;
using System.Text.Json;
using Container = Microsoft.Azure.Cosmos.Container;
using Console = Spectre.Console.AnsiConsole;

namespace AttributeArray.Services;

internal sealed class ProductService
{
    private readonly Container _productsContainer;

    public ProductService(Container productsContainer)
    {
        _productsContainer = productsContainer;
    }

    public async Task GenerateProductsAsync()
    {
        
        (IList<AttributePropertyProduct> attributePropertyProducts, IList<AttributeArrayProduct> attributeArrayProducts) = await GenerateDataAsync();

        await InsertAttributePropertyItemsAsync(attributePropertyProducts);

        await InsertAttributeArrayItemsAsync(attributeArrayProducts);

        await ExecuteAttributePropertyQueryAsync();

        await ExecuteAttributeArrayQueryAsync();
        

    }

    private async Task<(IList<AttributePropertyProduct> attributePropertyProducts, IList<AttributeArrayProduct> attributeArrayProducts)> GenerateDataAsync()
    {
        int count = 3;

        Console.MarkupLine($"[teal bold]Press any key to generate [underline]{count}[/] products with attributes as properties and attribute array pattern.[/]");
        System.Console.ReadKey();


        Console.MarkupLine($"[red italic]Creating [underline]{count}[/] product items with [bold]property-based attributes[/]...[/]");
        await Task.Delay(1500);

        IList<AttributePropertyProduct> attributePropertyProducts = new Faker<AttributePropertyProduct>()
            .CustomInstantiator(f =>
            {
                string identifier = $"{f.Random.Guid()}";
                return new AttributePropertyProduct(
                    Id: identifier,
                    ProductId: identifier,
                    Name: f.Commerce.ProductName(),
                    Category: f.Commerce.Department(),
                    Price: f.Commerce.Price(100, 1000, 2),
                    SizeSmall: f.Random.Int(1, 100),
                    SizeMedium: f.Random.Int(1, 100),
                    SizeLarge: f.Random.Int(1, 100),
                    EntityType: "Attribute Properties"
                );
            })
            .Generate(count)
            .AsReadOnly();

        //Make equivalent attribute array-based products
        List<AttributeArrayProduct> attributeArrayProducts = new List<AttributeArrayProduct>();

        Console.MarkupLine($"[red italic]Duplicating the [underline]{count}[/] products with [bold]array-based attributes[/]...[/]");
        await Task.Delay(1500);

        foreach (var attributePropertyProduct in attributePropertyProducts)
        {

            IList<ProductSize> productSizes = new List<ProductSize>();
            productSizes.Add(new ProductSize(Size: "Small", Count: attributePropertyProduct.SizeSmall));
            productSizes.Add(new ProductSize(Size: "Medium", Count: attributePropertyProduct.SizeMedium));
            productSizes.Add(new ProductSize(Size: "Large", Count: attributePropertyProduct.SizeLarge));

            attributeArrayProducts.Add(new AttributeArrayProduct
            (
                Id: Guid.NewGuid().ToString(),  //Need a unique id so it can co-exist in the same logical partition of productId
                ProductId: attributePropertyProduct.ProductId,
                Name: attributePropertyProduct.Name,
                Category: attributePropertyProduct.Category,
                Price: attributePropertyProduct.Price,
                EntityType: "Attribute Array",
                Sizes: productSizes
            ));

        }

        //Print out the new product names
        Console.Write(
            new Panel(
                new Rows(
                    attributePropertyProducts.Select(p => new Markup($"Product: [underline]{p.Name}[/]")).ToArray()
                )
            )
                .Header($"[teal bold]Products created[/]")
                .RoundedBorder()
                .BorderColor(Color.Teal)
            );

        return (attributePropertyProducts, attributeArrayProducts);

    }

    private async Task InsertAttributePropertyItemsAsync(IList<AttributePropertyProduct> attributePropertyProducts)
    {
        List<Task> upsertTasks = new();

        //Insert attribute property sample items
        foreach (var product in attributePropertyProducts)
        {
            upsertTasks.Add(
                _productsContainer.UpsertItemAsync<AttributePropertyProduct>(
                    item: product,
                    partitionKey: new PartitionKey(product.ProductId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);
    }

    private async Task InsertAttributeArrayItemsAsync(IList<AttributeArrayProduct> attributeArrayProducts)
    {
        List<Task> upsertTasks = new();

        //insert attribute array sample items
        foreach (var product in attributeArrayProducts)
        {
            upsertTasks.Add(
                _productsContainer.UpsertItemAsync<AttributeArrayProduct>(
                    item: product,
                    partitionKey: new PartitionKey(product.ProductId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);
    }

    private async Task ExecuteAttributePropertyQueryAsync()
    {
        Console.MarkupLine($"[teal bold]Press any key to execute a query with attributes as properties[/]");
        System.Console.ReadKey();


        Console.MarkupLine("[red italic]Performing a query on size attributes using multiple [underline]OR[/] statements...[/]");
        await Task.Delay(1500);

        string queryString = "SELECT p.name, p.sizeSmall, p.sizeMedium, p.sizeLarge FROM products p WHERE p.sizeSmall >= @quantity OR p.sizeMedium >= @quantity OR p.sizeLarge >= @quantity";

        int inputQuantity = 75;
        var query = new QueryDefinition(queryString)
            .WithParameter("@quantity", inputQuantity);

        using FeedIterator<PropertyQueryResult> feed = _productsContainer.GetItemQueryIterator<PropertyQueryResult>(query);

        List<PropertyQueryResult> items = new();
        while (feed.HasMoreResults)
        {
            FeedResponse<PropertyQueryResult> response = await feed.ReadNextAsync();

            items.AddRange(response.Resource);
        }

        string listJson = JsonSerializer.Serialize(items);
        Console.Write(
            new Panel(
                new Rows(
                    new Markup($"[teal][italic]@quantity[/]: {inputQuantity}[/]"),
                    new Markup($"[teal]{queryString}[/]"),
                    new Markup($"Matched [underline]{items.Count}[/] products"),
                    new JsonText(listJson)
                )
            )
            .Header("[bold]Query results[/]")
            .RoundedBorder()
            .Expand()
            .BorderColor(Color.Teal)
        );

    }

    private async Task ExecuteAttributeArrayQueryAsync()
    {
        Console.MarkupLine($"[teal bold]Press any key to execute a query using attribute array pattern[/]");
        System.Console.ReadKey();

        Console.MarkupLine("[red italic]Performing a query on size attributes using simple [underline]JOIN[/] statements and comparison operators...[/]");
        await Task.Delay(1500);

        string queryString = $"SELECT p.name, s.size, s.count FROM products p JOIN s IN p.sizes WHERE s.count >= @quantity";

        int inputQuantity = 75;
        var query = new QueryDefinition(queryString)
            .WithParameter("@quantity", inputQuantity);

        using FeedIterator<ArrayQueryResult> feed = _productsContainer.GetItemQueryIterator<ArrayQueryResult>(query);

        List<ArrayQueryResult> items = new();
        while (feed.HasMoreResults)
        {
            FeedResponse<ArrayQueryResult> response = await feed.ReadNextAsync();

            items.AddRange(response.Resource);
        }

        string listJson = JsonSerializer.Serialize(items);
        Console.Write(
            new Panel(
                new Rows(
                    new Markup($"[teal][italic]@quantity[/]: {inputQuantity}[/]"),
                    new Markup($"Found [underline]{items.Count}[/] products"),
                    new Markup($"[teal]{queryString}[/]"),
                    new JsonText(listJson)
                )
            )
                .Header("[bold]Query results[/]")
                .RoundedBorder()
                .Expand()
                .BorderColor(Color.Teal)
        );
    }

}