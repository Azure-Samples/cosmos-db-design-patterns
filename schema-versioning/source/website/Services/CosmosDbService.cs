using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Versioning.Services 
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public Container CartsContainer => _container;

        public CosmosDbService(string cosmosUri, string? cosmosKey, string databaseName, string containerName, string partitionKeyPath)
        {
            // Prefer keyless authentication via DefaultAzureCredential (managed identity / Azure CLI).
            // Fall back to key-based authentication only when CosmosKey is explicitly set (e.g. local emulator).
            // When targeting the local emulator (localhost), use Gateway mode and accept its
            // self-signed certificate. This only ever applies to a local emulator endpoint.
            CosmosClientOptions? clientOptions = null;
            if (!string.IsNullOrEmpty(cosmosUri) && new Uri(cosmosUri).Host is "localhost" or "127.0.0.1")
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

            _client = string.IsNullOrEmpty(cosmosKey)
                ? new CosmosClient(accountEndpoint: cosmosUri, tokenCredential: new DefaultAzureCredential(), clientOptions: clientOptions)
                : new CosmosClient(accountEndpoint: cosmosUri, authKeyOrResourceToken: cosmosKey, clientOptions: clientOptions);

            // Container handle is a lightweight proxy (no network call); usable once the
            // database/container exist.
            _container = _client.GetContainer(databaseName, containerName);

            // Ensure the database/container exist in the background rather than blocking (or
            // crashing) the app at startup while waiting on Cosmos DB. The call is retried to
            // tolerate transient startup conditions such as managed-identity role propagation
            // when deployed to Azure. When the account already has them (for example, provisioned
            // by the azd/Bicep deployment) these calls simply read and return.
            _ = EnsureCreatedAsync(databaseName, containerName, partitionKeyPath);
        }

        private async Task EnsureCreatedAsync(string databaseName, string containerName, string partitionKeyPath)
        {
            const int maxAttempts = 30;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Database database = await _client.CreateDatabaseIfNotExistsAsync(id: databaseName);
                    await database.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 3, 30)));
                }
            }
        }
    }
}
