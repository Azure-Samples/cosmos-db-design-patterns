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
            .AddJsonFile("appsettings.development.json", optional: true);

            _config = configuration
                .Build()
                .Get<Cosmos>();


            _client = new CosmosClient(_config?.CosmosUri, _config?.CosmosKey);

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