using Azure.Identity;
using MaterializedViews.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace MaterializedViews
{
    internal class Program
    {
       
        static Database? database;
        static Container? container;

        static string databaseName = "MaterializedViewsDB";
        static string containerName = "Sales";
        static string partitionKeyPath = "/CustomerId";

        static void Main(string[] args)
        {

            IConfigurationBuilder configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.development.json", optional: true);

            Cosmos? config = configuration
                .Build()
                .Get<Cosmos>();


            Console.WriteLine("This code will generate sample sales in the MaterializedViewsDB in the Sales container with a partition key of /CustomerId.");
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();

            // Prefer keyless authentication via DefaultAzureCredential (managed identity / Azure CLI).
            // Fall back to key-based authentication only when CosmosKey is explicitly set (e.g. local emulator).
            CosmosClient client = string.IsNullOrEmpty(config?.CosmosKey)
                ? new CosmosClient(
                    accountEndpoint: config?.CosmosUri,
                    tokenCredential: new DefaultAzureCredential())
                : new CosmosClient(
                    accountEndpoint: config?.CosmosUri,
                    authKeyOrResourceToken: config?.CosmosKey);

            database = client.CreateDatabaseIfNotExistsAsync(id: databaseName).Result;
            container = database.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath).Result;

            string userInput;
           
            do {

                Console.WriteLine("How many sales records should be created?");
                userInput = Console.ReadLine()!;

                int.TryParse(userInput, out int numOfSales);

                for (int i = 0; i < numOfSales; i++)
                {
                var order = SalesHelper.GenerateSales();

                container.CreateItemAsync<Sales>(
                    item: order, 
                    partitionKey: new PartitionKey(order.CustomerId)).Wait();

                }

                Console.WriteLine("Add more records? [y/N]");
                userInput = Console.ReadLine()!;

            } while (userInput != null && userInput.ToUpper() == "Y");

            Console.WriteLine($"Check {containerName} for new Sales");
            
            Console.WriteLine("Press Enter to exit.");
            Console.ReadKey();
        }
    }
}