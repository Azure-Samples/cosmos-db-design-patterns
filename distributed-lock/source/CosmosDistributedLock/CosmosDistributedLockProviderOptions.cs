using Microsoft.Azure.Cosmos;

namespace Cosmos.DistributedLock
{
    public class CosmosDistributedLockProviderOptions
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
