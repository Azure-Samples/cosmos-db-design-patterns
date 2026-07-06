// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock

namespace CosmosDistributedLock
{
    public interface ICloudDistributedLockProvider
    {
        /// <summary>Tries to acquire the lock once and returns immediately.</summary>
        Task<CloudDistributedLock> TryAcquireLockAsync(string name);

        /// <summary>Waits (indefinitely, or up to <paramref name="timeout"/>) for the lock.</summary>
        Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = default);
    }

    internal class CloudDistributedLockProvider : ICloudDistributedLockProvider
    {
        private readonly CloudDistributedLockProviderOptions options;
        private readonly ICosmosLockClient cosmosLockClient;

        public CloudDistributedLockProvider(CloudDistributedLockProviderOptions options)
        {
            this.options = options;
            cosmosLockClient = new CosmosLockClient(options);
        }

        internal CloudDistributedLockProvider(ICosmosLockClient cosmosLockClient, CloudDistributedLockProviderOptions options)
        {
            this.options = options;
            this.cosmosLockClient = cosmosLockClient;
        }

        public async Task<CloudDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = null)
        {
            using var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            return await ContinuallyTryAcquireLockAsync(name, cancellationTokenSource.Token).ConfigureAwait(false);
        }

        public async Task<CloudDistributedLock> TryAcquireLockAsync(string name)
        {
            var item = await cosmosLockClient.TryAcquireLockAsync(name).ConfigureAwait(false);
            if (item != null)
            {
                return CloudDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            }
            else
            {
                return CloudDistributedLock.CreateUnacquiredLock();
            }
        }

        private async Task<CloudDistributedLock> ContinuallyTryAcquireLockAsync(string name, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var @lock = await TryAcquireLockAsync(name).ConfigureAwait(false);
                if (@lock.IsAcquired)
                {
                    return @lock;
                }

                @lock.Dispose();

                try
                {
                    await Task.Delay(options.RetryInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return CloudDistributedLock.CreateUnacquiredLock();
                }
            }

            return CloudDistributedLock.CreateUnacquiredLock();
        }
    }
}
