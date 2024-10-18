using Microsoft.Azure.Cosmos;

namespace Services
{
    public class CosmosDb
    {
        private readonly CosmosClient client;
        private Container? orderContainer;
        private Container? historyContainer;
        private Container? leasesContainer;

        public Container OrderContainer => orderContainer ?? throw new InvalidOperationException("OrderContainer is not initialized.");
        public Container HistoryContainer => historyContainer ?? throw new InvalidOperationException("HistoryContainer is not initialized.");
        public Container LeasesContainer => leasesContainer ?? throw new InvalidOperationException("LeasesContainer is not initialized.");

        public CosmosDb(string cosmosUri, string cosmosKey, string database, string currentOrderContainer, string historicalOrderContainer, string partitionKey)
        {
           
            client = new CosmosClient(
                accountEndpoint: cosmosUri!,
                authKeyOrResourceToken: cosmosKey!);

            InitializeAsync(database, currentOrderContainer, historicalOrderContainer, partitionKey).Wait();
        }

        private async Task InitializeAsync(string databaseName, string currentOrderContainerName, string historicalOrderContainerName, string partitionKey)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            orderContainer = await database.CreateContainerIfNotExistsAsync(
                id: currentOrderContainerName,
                partitionKeyPath: partitionKey
            );

            historyContainer = await database.CreateContainerIfNotExistsAsync(
                id: historicalOrderContainerName,
                partitionKeyPath: partitionKey
            );

            leasesContainer = await database.CreateContainerIfNotExistsAsync(
                id: "leases",
                partitionKeyPath: "/id"
            );
        }

        
    }
}
