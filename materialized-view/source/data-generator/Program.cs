using Microsoft.Azure.Cosmos;

namespace MaterializedViews 
{
    internal class Program
    {
       
        static Database? db;

        static Container? container;

        static string partitionKeyPath = "/CustomerId";

        static void Main(string[] args)
        {
            
            Console.WriteLine("This code will generate sample sales and create them in an Azure Cosmos DB for NoSQL account.");
            Console.WriteLine($"The primary key for this container will be {partitionKeyPath}.\n\n");

            Console.WriteLine("Enter the database name [default:Sales]:");
            string? userInput = Console.ReadLine();
            
            string databaseName = string.IsNullOrWhiteSpace(userInput) ? "Sales" : userInput;

            CosmosClient client = new(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);

            db = client.CreateDatabaseIfNotExistsAsync(databaseName).Result;

            Console.WriteLine("Enter the container name [default:Sales]:");
            userInput = Console.ReadLine();

            string containerName = string.IsNullOrWhiteSpace(userInput) ? "Sales" : userInput;
           
            do {

                Console.WriteLine("How many sales records should be created?");
                userInput = Console.ReadLine();

                int.TryParse(userInput, out int numOfSales);
                container = db.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath, throughput: 400).Result;

                for (int i = 0; i < numOfSales; i++)
                {
                var order = SalesHelper.GenerateSales();
                container.CreateItemAsync(order).Wait();              
                }

                Console.WriteLine("Add more records? [y/N]");
                userInput = Console.ReadLine();
            } while (userInput != null && userInput.ToUpper() == "Y");

            Console.WriteLine($"Check {containerName} for new Sales");
            
            Console.WriteLine("Press Enter to exit.");
            Console.ReadKey();
        }
    }
}