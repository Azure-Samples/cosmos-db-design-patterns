using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Cosmos.DistributedLock
{
    public interface ICosmosDistributedLockProviderFactory
    {
        ICosmosDistributedLockProvider GetLockProvider();
        ICosmosDistributedLockProvider GetLockProvider(string name);
    }

    public class CosmosDistributedLockProviderFactory : ICosmosDistributedLockProviderFactory
    {
        internal const string DefaultName = "__DEFAULT";

        private readonly ConcurrentDictionary<string, ICosmosDistributedLockProvider> clients = new();

        public CosmosDistributedLockProviderFactory(IOptionsMonitor<CosmosDistributedLockProviderOptions> optionsMonitor)
        {
            OptionsMonitor = optionsMonitor;
        }

        protected IOptionsMonitor<CosmosDistributedLockProviderOptions> OptionsMonitor { get; }

        public ICosmosDistributedLockProvider GetLockProvider(string name)
        {
            return clients.GetOrAdd(name, n => CreateClient(n));
        }

        public ICosmosDistributedLockProvider GetLockProvider()
        {
            return GetLockProvider(DefaultName);
        }

        protected ICosmosDistributedLockProvider CreateClient(string name)
        {
            var options = OptionsMonitor.Get(name);

            ArgumentNullException.ThrowIfNull(options.ProviderName);
            ArgumentNullException.ThrowIfNull(options.CosmosClient);
            ArgumentNullException.ThrowIfNull(options.DatabaseName);
            ArgumentNullException.ThrowIfNull(options.ContainerName);

            return new CosmosDistributedLockProvider(options);
        }
    }
}
