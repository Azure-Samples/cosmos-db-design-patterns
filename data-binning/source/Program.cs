using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


namespace DataBinning
{

    internal class Program 
    {
        private static object? _lock;

        static async Task Main(string[] args)
        {
            Console.WriteLine("This code will generate sensor events and bucket them by minute before saving to an Azure Cosmos DB for NoSQL account.");
       
            Container container = await createCosmosDBArtifactsAsync();

            Console.WriteLine("How many devices would you like to generate data for (enter number between 1 and 25)?");
            var deviceCountInput = Console.ReadLine();
            int.TryParse(deviceCountInput, out int deviceCount);
           if (deviceCount > 25 )
           {
                Console.WriteLine("Reducing devices to 25");
                deviceCount = 25;
           }

            Console.WriteLine("How many minutes would you like to generate data for (enter number between 1 and 10)?");
            var timeoutInput = Console.ReadLine();            
            int.TryParse(timeoutInput, out int timeout);
            if (timeout > 10)
            {
                Console.WriteLine("Reducing minutes to 10");
                timeout = 10;
            }

            var currtime = System.DateTime.UtcNow;
            var finalBatchtime = Utility.GetNextPublishTime(currtime.AddMinutes(timeout));
            int durationSec = (int) (finalBatchtime - currtime).Duration().TotalSeconds;

            Console.WriteLine($"Please wait while events are simulated for {timeout} minutes.");

            _lock = new object();
            for (int i = 0; i < deviceCount; i++)
            {
                WorkerThread wt = new WorkerThread(
                    timeout,
                    container,
                    (i+1).ToString(),
                     new PostMessageCallback(MessageCallback));

                Task asyncTask = Task.Run(() => wt.SimulateEvents());
            }

            //wait till all threads complete.
            await Task.Delay((durationSec +2) * 1000);

            Console.WriteLine($"Completed generation events for {deviceCount} devices");
            Console.WriteLine($"Check DataBinning Container for sensor events");
        }

        private async static Task<Container> createCosmosDBArtifactsAsync()
        {
            
            Console.WriteLine($"Please wait while Cosmos DB database and container is created.");

            var configuration = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                 .AddJsonFile($"appsettings.development.json", optional: true);

            var config = configuration.Build();

            string uri = config["CosmosUri"]!;
            string key = config["CosmosKey"]!;

            CosmosClient client = new(accountEndpoint: uri, authKeyOrResourceToken: key);

            Database database = await client.CreateDatabaseIfNotExistsAsync(
                    id: "DataBinningDB"
                );

            string partitionKeyPath = "/DeviceId";
            Container container = await database.CreateContainerIfNotExistsAsync(
                    id: "DataBinning",
                    partitionKeyPath: partitionKeyPath
                ); 

            return container;

        }

        private static void MessageCallback(string message)
        {
            lock (_lock!)
            {
                Console.WriteLine(message);
            }
        }
    }
}
