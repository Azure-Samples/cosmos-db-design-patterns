// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock
//
// This version adds an endpoint/key overload that builds the CosmosClient the same way as the
// rest of this repository (see CosmosClientFactory): keyless when no key is supplied, and
// accepting the local emulator's self-signed certificate when the endpoint is localhost.

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosDistributedLock
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCloudDistributedLock(this IServiceCollection services, string cosmosEndpoint, string? cosmosKey, string databaseName, int ttl, Action<CloudDistributedLockProviderOptions>? configureOptions = null)
        {
            return services.AddCloudDistributedLock(CloudDistributedLockProviderFactory.DefaultName, CosmosClientFactory.Create(cosmosEndpoint, cosmosKey), databaseName, ttl, configureOptions);
        }

        public static IServiceCollection AddCloudDistributedLock(this IServiceCollection services, CosmosClient cosmosClient, string databaseName, int ttl, Action<CloudDistributedLockProviderOptions>? configureOptions = null)
        {
            return services.AddCloudDistributedLock(CloudDistributedLockProviderFactory.DefaultName, cosmosClient, databaseName, ttl, configureOptions);
        }

        public static IServiceCollection AddCloudDistributedLock(this IServiceCollection services, string name, string cosmosEndpoint, string? cosmosKey, string databaseName, int ttl, Action<CloudDistributedLockProviderOptions>? configureOptions = null)
        {
            return services.AddCloudDistributedLock(name, CosmosClientFactory.Create(cosmosEndpoint, cosmosKey), databaseName, ttl, configureOptions);
        }

        public static IServiceCollection AddCloudDistributedLock(this IServiceCollection services, string name, CosmosClient cosmosClient, string databaseName, int ttl, Action<CloudDistributedLockProviderOptions>? configureOptions = null)
        {
            services.AddOptions();
            services.AddSingleton<ICloudDistributedLockProviderFactory, CloudDistributedLockProviderFactory>();
            services.Configure<CloudDistributedLockProviderOptions>(name, o =>
            {
                o.ProviderName = name;
                o.CosmosClient = cosmosClient;
                o.DatabaseName = databaseName;
                o.TTL = ttl;
                configureOptions?.Invoke(o);
            });
            return services;
        }
    }
}

