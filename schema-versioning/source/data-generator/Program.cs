using Microsoft.Azure.Cosmos;
using System.ComponentModel.DataAnnotations;

namespace Versioning 
{
    internal class Program
    {
       
        static Database? db;

        static Container? container;

        static string partitionKeyPath = "/id";

        static void Main(string[] args)
        {
            
            Console.WriteLine("This code will generate sample carts and create them in an Azure Cosmos DB for NoSQL account.");
            Console.WriteLine("The primary key for this container will be /id.\n\n");

            Console.WriteLine("Enter the database name [default:CartsDemo]:");
            string? userInput = Console.ReadLine();
            
            string databaseName = string.IsNullOrWhiteSpace(userInput) ? "CartsDemo" : userInput;

            CosmosClient client = new(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);

            db = client.CreateDatabaseIfNotExistsAsync(databaseName).Result;

            Console.WriteLine("Enter the container name [default:Carts]:");
            userInput = Console.ReadLine();

            string containerName = string.IsNullOrWhiteSpace(userInput) ? "Carts" : userInput;
           
            Console.WriteLine("How many carts should be created?");
            userInput = Console.ReadLine();

            int.TryParse(userInput, out int numOfCarts);
            container = db.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath, throughput: 400).Result;
            for (int i = 0; i < numOfCarts; i++)
            {
              var cart = CartHelper.GenerateCart();
              container.UpsertItemAsync(cart).Wait();
            }

            for (int i = 0; i < numOfCarts; i++)
            {
            var cart = CartHelper.GenerateVersionedCart();
                    container.UpsertItemAsync(cart).Wait();
            }
            Console.WriteLine($"Check {containerName} for new carts");
            
            Console.WriteLine("Press Enter to exit.");
            Console.ReadKey();
        }
    }
}