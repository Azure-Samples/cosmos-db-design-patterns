using Microsoft.Azure.Cosmos;

namespace Versioning {
    public class CosmosHelper
    {
        CosmosClient client;

        private static string DatabaseName = "CartsDemo";
        private static string ContainerName = "Carts";
        private static string PartitionKey = "/id";

        public CosmosHelper()
        {
            client = new(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);
        }

        async public Task<Database> GetDatabase()
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(
                id: DatabaseName
            );

            return database;
        }

        async public Task<Container> GetContainer()
        {
            Database database = await GetDatabase();

            Container container = await database.CreateContainerIfNotExistsAsync(
                id: ContainerName,
                partitionKeyPath: PartitionKey,
                throughput: 400
            );

            return container;
        }        
    }
}