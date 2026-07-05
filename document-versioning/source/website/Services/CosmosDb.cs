using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace Services
{
    public class CosmosDb
    {
        private readonly CosmosClient client;
        private readonly string databaseName;
        private readonly string currentOrderContainerName;
        private readonly string historicalOrderContainerName;
        private readonly string partitionKey;

        // Container handles are lightweight proxies (no network call), so they are safe to
        // create up front. They become usable once the database/containers exist.
        public Container OrderContainer { get; }
        public Container HistoryContainer { get; }
        public Container LeasesContainer { get; }

        public CosmosDb(string cosmosUri, string? cosmosKey, string database, string currentOrderContainer, string historicalOrderContainer, string partitionKey)
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

            client = string.IsNullOrEmpty(cosmosKey)
                ? new CosmosClient(accountEndpoint: cosmosUri, tokenCredential: new DefaultAzureCredential(), clientOptions: clientOptions)
                : new CosmosClient(accountEndpoint: cosmosUri, authKeyOrResourceToken: cosmosKey, clientOptions: clientOptions);

            databaseName = database;
            currentOrderContainerName = currentOrderContainer;
            historicalOrderContainerName = historicalOrderContainer;
            this.partitionKey = partitionKey;

            OrderContainer = client.GetContainer(database, currentOrderContainer);
            HistoryContainer = client.GetContainer(database, historicalOrderContainer);
            LeasesContainer = client.GetContainer(database, "leases");
        }

        /// <summary>
        /// Ensures the database and containers exist. Called in the background at startup rather
        /// than synchronously in the constructor, so the app does not block (or crash) while
        /// waiting on Cosmos DB. The call is retried to tolerate transient startup conditions such
        /// as managed-identity token/role-assignment propagation when deployed to Azure.
        /// When the account already has the database/containers (for example, provisioned by the
        /// azd/Bicep deployment) these calls simply read and return.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            const int maxAttempts = 30;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken);

                    await database.CreateContainerIfNotExistsAsync(
                        id: currentOrderContainerName,
                        partitionKeyPath: partitionKey,
                        cancellationToken: cancellationToken);

                    await database.CreateContainerIfNotExistsAsync(
                        id: historicalOrderContainerName,
                        partitionKeyPath: partitionKey,
                        cancellationToken: cancellationToken);

                    await database.CreateContainerIfNotExistsAsync(
                        id: "leases",
                        partitionKeyPath: "/id",
                        cancellationToken: cancellationToken);

                    return;
                }
                catch (Exception) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    // Back off (capped) and retry; covers transient auth/connectivity at startup.
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 3, 30)), cancellationToken);
                }
            }
        }
    }
}
