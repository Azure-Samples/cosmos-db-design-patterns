// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock

using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace CosmosDistributedLock
{
    /// <summary>
    /// Builds a <see cref="CosmosClient"/> the same way as the rest of this repository:
    /// keyless (DefaultAzureCredential) when no key is supplied, or key-based otherwise. When
    /// the endpoint is the local emulator (localhost), it uses Gateway mode and accepts the
    /// emulator's self-signed certificate — this only ever applies to a local emulator endpoint.
    /// </summary>
    public static class CosmosClientFactory
    {
        public static CosmosClient Create(string cosmosEndpoint, string? cosmosKey)
        {
            CosmosClientOptions? clientOptions = null;
            if (!string.IsNullOrEmpty(cosmosEndpoint) && new Uri(cosmosEndpoint).Host is "localhost" or "127.0.0.1")
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

            return string.IsNullOrEmpty(cosmosKey)
                ? new CosmosClient(cosmosEndpoint, new DefaultAzureCredential(), clientOptions)
                : new CosmosClient(cosmosEndpoint, cosmosKey, clientOptions);
        }
    }
}
