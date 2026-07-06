namespace Cosmos.DistributedLock
{
    public interface ICosmosDistributedLockProvider
    {
        /// <summary>Tries to acquire the lock once and returns immediately.</summary>
        Task<CosmosDistributedLock> TryAcquireLockAsync(string name);

        /// <summary>Waits (indefinitely, or up to <paramref name="timeout"/>) for the lock.</summary>
        Task<CosmosDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = default);
    }

    internal class CosmosDistributedLockProvider : ICosmosDistributedLockProvider
    {
        private readonly CosmosDistributedLockProviderOptions options;
        private readonly ICosmosLockClient cosmosLockClient;

        public CosmosDistributedLockProvider(CosmosDistributedLockProviderOptions options)
        {
            this.options = options;
            cosmosLockClient = new CosmosLockClient(options);
        }

        internal CosmosDistributedLockProvider(ICosmosLockClient cosmosLockClient, CosmosDistributedLockProviderOptions options)
        {
            this.options = options;
            this.cosmosLockClient = cosmosLockClient;
        }

        public async Task<CosmosDistributedLock> AcquireLockAsync(string name, TimeSpan? timeout = null)
        {
            using var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            return await ContinuallyTryAcquireLockAsync(name, cancellationTokenSource.Token).ConfigureAwait(false);
        }

        public async Task<CosmosDistributedLock> TryAcquireLockAsync(string name)
        {
            var item = await cosmosLockClient.TryAcquireLockAsync(name).ConfigureAwait(false);
            if (item != null)
            {
                return CosmosDistributedLock.CreateAcquiredLock(cosmosLockClient, item);
            }
            else
            {
                return CosmosDistributedLock.CreateUnacquiredLock();
            }
        }

        private async Task<CosmosDistributedLock> ContinuallyTryAcquireLockAsync(string name, CancellationToken cancellationToken)
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
                    return CosmosDistributedLock.CreateUnacquiredLock();
                }
            }

            return CosmosDistributedLock.CreateUnacquiredLock();
        }
    }
}
