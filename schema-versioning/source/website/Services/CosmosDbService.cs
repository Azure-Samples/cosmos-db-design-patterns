using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Versioning.Services 
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public Container CartsContainer => _container ?? throw new InvalidOperationException("Carts Container is not initialized.");

        public CosmosDbService(string cosmosUri, string? cosmosKey, string databaseName, string containerName, string partitionKeyPath)
        {
            // Prefer keyless authentication via DefaultAzureCredential (managed identity / Azure CLI).
            // Fall back to key-based authentication only when CosmosKey is explicitly set (e.g. local emulator).
            _client = string.IsNullOrEmpty(cosmosKey)
                ? new CosmosClient(accountEndpoint: cosmosUri, tokenCredential: new DefaultAzureCredential())
                : new CosmosClient(accountEndpoint: cosmosUri, authKeyOrResourceToken: cosmosKey);

_container = InitializeAsync(databaseName, containerName, partitionKeyPath).GetAwaiter().GetResult();
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