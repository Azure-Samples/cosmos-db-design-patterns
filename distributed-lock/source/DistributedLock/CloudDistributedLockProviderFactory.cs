// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock

using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CosmosDistributedLock
{
    public interface ICloudDistributedLockProviderFactory
    {
        ICloudDistributedLockProvider GetLockProvider();
        ICloudDistributedLockProvider GetLockProvider(string name);
    }

    public class CloudDistributedLockProviderFactory : ICloudDistributedLockProviderFactory
    {
        internal const string DefaultName = "__DEFAULT";

        private readonly ConcurrentDictionary<string, ICloudDistributedLockProvider> clients = new();

        public CloudDistributedLockProviderFactory(IOptionsMonitor<CloudDistributedLockProviderOptions> optionsMonitor)
        {
            OptionsMonitor = optionsMonitor;
        }

        protected IOptionsMonitor<CloudDistributedLockProviderOptions> OptionsMonitor { get; }

        public ICloudDistributedLockProvider GetLockProvider(string name)
        {
            return clients.GetOrAdd(name, n => CreateClient(n));
        }

        public ICloudDistributedLockProvider GetLockProvider()
        {
            return GetLockProvider(DefaultName);
        }

        protected ICloudDistributedLockProvider CreateClient(string name)
        {
            var options = OptionsMonitor.Get(name);

            ArgumentNullException.ThrowIfNull(options.ProviderName);
            ArgumentNullException.ThrowIfNull(options.CosmosClient);
            ArgumentNullException.ThrowIfNull(options.DatabaseName);
            ArgumentNullException.ThrowIfNull(options.ContainerName);

            return new CloudDistributedLockProvider(options);
        }
    }
}
