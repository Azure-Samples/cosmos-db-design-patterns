using Bogus;
using DataUploader.Models;
using Microsoft.Azure.Cosmos;
using Spectre.Console;
using Spectre.Console.Json;
using System.Text.Json;
using Container = Microsoft.Azure.Cosmos.Container;
using Console = Spectre.Console.AnsiConsole;

namespace DataUploader.Services;

internal sealed class ProductService
{
    private readonly Container _productsContainer;

    public ProductService(Container productsContainer)
    {
        _productsContainer = productsContainer;
    }

    public async Task GenerateProductsAsync()
    {
        // Property-based
        await GeneratePropertyBasedAttributeProductsAsync();

        // Array-based
        await GenerateArrayBasedAttributeProductsAsync();
    }

    private async Task GeneratePropertyBasedAttributeProductsAsync()
    {
        int count = 3;

        Console.MarkupLine($"[red italic]Creating [underline]{count}[/] product items with [bold]property-based attributes[/]...[/]");
        await Task.Delay(1500);

        IReadOnlyCollection<AttributePropertyProduct> products = new Faker<AttributePropertyProduct>()
            .CustomInstantiator(f =>
                {
                    string identifier = $"{f.Random.Guid()}";
                    return new AttributePropertyProduct(
                        Id: identifier,
                        ProductId: identifier,
                        Name: f.Commerce.ProductName(),
                        Category: f.Commerce.Department(),
                        Price: f.Random.Decimal(100, 1000),
                        SizeSmall: f.Random.Int(1, 100),
                        SizeMedium: f.Random.Int(1, 100),
                        SizeLarge: f.Random.Int(1, 100)
                    );
                })
            .Generate(count)
            .AsReadOnly();

        List<Task> upsertTasks = new();
        foreach (var product in products)
        {
            upsertTasks.Add(
                _productsContainer.UpsertItemAsync<AttributePropertyProduct>(
                    item: product,
                    partitionKey: new PartitionKey(product.ProductId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);

        Console.Write(
            new Panel(
                new Rows(
                    products.Select(p => new Markup($"New product: [underline]{p.Id}[/]")).ToArray()
                )
            )
                .Header($"[teal bold]Products created[/]")
                .RoundedBorder()
                .BorderColor(Color.Teal)
        );

        Console.MarkupLine("[red italic]Performing a query on all attributes using multiple [underline]OR[/] statements...[/]");
        await Task.Delay(1500);

        string queryString = "SELECT VALUE p FROM products p WHERE p.sizeSmall >= @quantity OR p.sizeMedium >= @quantity OR p.sizeLarge >= @quantity";

        int inputQuantity = 75;
        var query = new QueryDefinition(queryString)
            .WithParameter("@quantity", inputQuantity);

        using FeedIterator<AttributePropertyProduct> feed = _productsContainer.GetItemQueryIterator<AttributePropertyProduct>(query);

        List<AttributePropertyProduct> items = new();
        while (feed.HasMoreResults)
        {
            FeedResponse<AttributePropertyProduct> response = await feed.ReadNextAsync();

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

    private async Task GenerateArrayBasedAttributeProductsAsync()
    {
        int count = 3;

        Console.MarkupLine($"[red italic]Creating [underline]{count}[/] product items with [bold]array-based attributes[/]...[/]");
        await Task.Delay(1500);

        IReadOnlyCollection<AttributeArrayProduct> products = new Faker<AttributeArrayProduct>()
            .CustomInstantiator(f =>
                {
                    string identifier = $"{f.Random.Guid()}";
                    return new AttributeArrayProduct(
                        Id: identifier,
                        ProductId: identifier,
                        Name: f.Commerce.ProductName(),
                        Category: f.Commerce.Department(),
                        Price: f.Random.Decimal(100, 1000),
                        Sizes: new List<ProductSize>
                        {
                            new ProductSize("Small", f.Random.Int(1, 100)),
                            new ProductSize("Medium", f.Random.Int(1, 100)),
                            new ProductSize("Large", f.Random.Int(1, 100))
                        }
                    );
                })
            .Generate(count)
            .AsReadOnly();

        List<Task> upsertTasks = new();
        foreach (var product in products)
        {
            upsertTasks.Add(
                _productsContainer.UpsertItemAsync<AttributeArrayProduct>(
                    item: product,
                    partitionKey: new PartitionKey(product.ProductId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);

        Console.Write(
            new Panel(
                new Rows(
                    products.Select(p => new Markup($"New product: [underline]{p.Id}[/]")).ToArray()
                )
            )
                .Header($"[teal bold]Products created[/]")
                .RoundedBorder()
                .BorderColor(Color.Teal)
        );

        Console.MarkupLine("[red italic]Performing a query on attributes using simple [underline]JOIN[/] statements and comparison operators...[/]");
        await Task.Delay(1500);

        string queryString = $"SELECT VALUE p FROM products p JOIN s IN p.sizes WHERE s.count >= @quantity";

        int inputQuantity = 75;
        var query = new QueryDefinition(queryString)
            .WithParameter("@quantity", inputQuantity);

        using FeedIterator<AttributeArrayProduct> feed = _productsContainer.GetItemQueryIterator<AttributeArrayProduct>(query);

        List<AttributeArrayProduct> items = new();
        while (feed.HasMoreResults)
        {
            FeedResponse<AttributeArrayProduct> response = await feed.ReadNextAsync();

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