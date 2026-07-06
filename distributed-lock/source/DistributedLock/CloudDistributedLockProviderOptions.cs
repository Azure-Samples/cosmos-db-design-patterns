// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock

using Microsoft.Azure.Cosmos;

namespace CosmosDistributedLock
{
    public class CloudDistributedLockProviderOptions
    {
        internal string? ProviderName { get; set; }

        internal CosmosClient? CosmosClient { get; set; }

        /// <summary>Lock time-to-live in seconds. Should match the container's TTL configuration.</summary>
        public int TTL { get; set; } = 5;

        public string? DatabaseName { get; set; }

        public string ContainerName { get; set; } = "Locks";

        /// <summary>How often to retry when waiting on <c>AcquireLockAsync</c>.</summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
