using DataUploader.Options;
using DataUploader.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Container = Microsoft.Azure.Cosmos.Container;
using Database = Microsoft.Azure.Cosmos.Database;
using Console = Spectre.Console.AnsiConsole;

IConfigurationBuilder configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.Development.json", optional: true);

Cosmos? config = configuration
    .Build()
    .Get<Cosmos>();

Console.MarkupLine($"[red italic]Connecting to Azure Cosmos DB account...[/]");

CosmosSerializationOptions serializerOptions = new()
{
    IgnoreNullValues = true,
    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
};

CosmosClientOptions options = new()
{
    AllowBulkExecution = true,
    SerializerOptions = serializerOptions,
    MaxRetryAttemptsOnRateLimitedRequests = 10
};

CosmosClient client = new(
    accountEndpoint: config?.CosmosUri,
    authKeyOrResourceToken: config?.CosmosKey,
    clientOptions: options
);

Database database = await client.CreateDatabaseIfNotExistsAsync(
    id: "CosmosPatterns",
    throughput: 400
);

Console.Write(
    new Panel("[green]Attribute upload utility featuring sample queries[/]")
        .BorderColor(Color.White)
        .RoundedBorder()
        .Expand()
);

Console.Write(
    new Rule("[yellow]Property and array-based product attributes[/]")
        .LeftJustified()
        .RuleStyle("olive")
);

Container productsContainer = await database.CreateContainerIfNotExistsAsync(
    id: "Products",
    partitionKeyPath: "/productId"
);

// Use product container as an example
await new ProductService(productsContainer)
    .GenerateProductsAsync();

Console.Write(
    new Rule("[yellow]Attribute and Non-attribute based hotel rooms[/]")
        .LeftJustified()
        .RuleStyle("olive")
);

Container hotelRoomsContainer = await database.CreateContainerIfNotExistsAsync(
    id: "Hotels",
    partitionKeyPath: "/hotelId"
);

// Use hotel room price container as an example
await new HotelService(hotelRoomsContainer)
    .GenerateHotelRoomsAsync();

Console.Write(
    new Panel("[green]The attribute upload utlity has finished. Use the Data Explorer in Azure Cosmos DB to run additional queries.[/]")
        .BorderColor(Color.White)
        .RoundedBorder()
        .Expand()
);
