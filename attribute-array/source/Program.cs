using AttributeArray.Options;
using AttributeArray.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Console = Spectre.Console.AnsiConsole;
using Container = Microsoft.Azure.Cosmos.Container;
using Database = Microsoft.Azure.Cosmos.Database;

IConfigurationBuilder configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.development.json", optional: true);

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
    id: "AttributeArrayDB"
);

Console.Write(
    new Panel("[green]Sample comparing Attribute Array Pattern to Property-based Attributes[/]")
        .BorderColor(Color.White)
        .RoundedBorder()
        .Expand()
);

Console.Write(
    new Rule("[yellow]Property and Array-based product attributes[/]")
        .LeftJustified()
        .RuleStyle("olive")
);

Container productsContainer = await database.CreateContainerIfNotExistsAsync(
    id: "ArrayAttributes",
    partitionKeyPath: "/productId"
);

// Use product container as an example
await new ProductService(productsContainer)
    .GenerateProductsAsync();

Console.Write(
    new Panel("[green]The attribute upload utlity has finished. Use the Data Explorer in Azure Cosmos DB to run additional queries.[/]")
        .BorderColor(Color.White)
        .RoundedBorder()
        .Expand()
);