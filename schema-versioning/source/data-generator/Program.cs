using Azure.Identity;
using data_generator.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Versioning
{
    internal class Program
    {

        static CosmosClient? _client;
        static Container? _container;
        static Cosmos? _config;

        static async Task Main(string[] args)
        {

            IConfigurationBuilder configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true)
            .AddEnvironmentVariables();

            _config = configuration
                .Build()
                .Get<Cosmos>();


            // Prefer keyless authentication via DefaultAzureCredential (managed identity / Azure CLI).
            // Fall back to key-based authentication only when CosmosKey is explicitly set (e.g. local emulator).
            // When targeting the local emulator (localhost), use Gateway mode and accept its
            // self-signed certificate. This only ever applies to a local emulator endpoint.
            CosmosClientOptions? clientOptions = null;
            if (!string.IsNullOrEmpty(_config?.CosmosUri) && new Uri(_config!.CosmosUri).Host is "localhost" or "127.0.0.1")
            {
                clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    })
                };
            }

            _client = string.IsNullOrEmpty(_config?.CosmosKey)
                ? new CosmosClient(_config?.CosmosUri, new DefaultAzureCredential(), clientOptions)
                : new CosmosClient(_config?.CosmosUri, _config?.CosmosKey, clientOptions);

            await InitializeDatabase();

            string userInput = string.Empty;
            Console.WriteLine("This code will generate sample carts and create them in an Azure Cosmos DB for NoSQL account.");
            
            Console.WriteLine("How many carts should be created?");
            userInput = Console.ReadLine()!;

            int.TryParse(userInput, out int numOfCarts);

            for (int i = 0; i < numOfCarts; i++)
            {
              var cart = CartHelper.GenerateCart();
              _container!.UpsertItemAsync(cart).Wait();
            }

            for (int i = 0; i < numOfCarts; i++)
            {
            var cart = CartHelper.GenerateVersionedCart();
                    _container!.UpsertItemAsync(cart).Wait();
            }
            Console.WriteLine($"Check {_config!.ContainerName} container for new carts");
            
            Console.WriteLine("Press Enter to exit.");
            Console.ReadKey();
        }

        async static Task InitializeDatabase()
        {
            Database database = await _client!.CreateDatabaseIfNotExistsAsync(id: _config?.DatabaseName!);

            _container = await database.CreateContainerIfNotExistsAsync(
                id: _config?.ContainerName!,
                partitionKeyPath: _config?.PartitionKeyPath);

        }
    }
}