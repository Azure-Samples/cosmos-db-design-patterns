using Microsoft.Azure.Cosmos;

namespace Versioning.Services 
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public Container CartsContainer => _container ?? throw new InvalidOperationException("Carts Container is not initialized.");

        public CosmosDbService(string cosmosUri, string cosmosKey, string databaseName, string containerName, string partitionKeyPath)
        {
            _client = new(
                accountEndpoint: cosmosUri,
                authKeyOrResourceToken: cosmosKey);

            _container = InitializeAsync(databaseName, containerName, partitionKeyPath).Result;
        }

        private async Task<Container> InitializeAsync(string databaseName, string containerName, string partitionKeyPath)
        {
            Database database = await _client.CreateDatabaseIfNotExistsAsync(id: databaseName);

            Container container = await database.CreateContainerIfNotExistsAsync(
                id: containerName,
                partitionKeyPath: partitionKeyPath
            );

            return container;
        }
    }
}