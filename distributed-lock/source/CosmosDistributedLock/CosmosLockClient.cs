using Microsoft.Azure.Cosmos;
using System.Net;

namespace Cosmos.DistributedLock
{
    internal interface ICosmosLockClient
    {
        Task<ItemResponse<LockRecord>?> TryAcquireLockAsync(string name);
        Task<ItemResponse<LockRecord>?> RenewLockAsync(ItemResponse<LockRecord> item);
        Task ReleaseLockAsync(ItemResponse<LockRecord> item);
    }

    internal class CosmosLockClient : ICosmosLockClient
    {
        private readonly CosmosDistributedLockProviderOptions options;
        private readonly Container container;

        public CosmosLockClient(CosmosDistributedLockProviderOptions options)
        {
            this.options = options;
            container = options.CosmosClient!.GetContainer(options.DatabaseName, options.ContainerName);
        }

        public async Task<ItemResponse<LockRecord>?> TryAcquireLockAsync(string name)
        {
            try
            {
                // Inserting the document succeeds only if no other process currently holds the
                // lock. The container has TTL enabled and the record carries a _ttl, so Cosmos
                // deletes it automatically (releasing the lock) if the holder stops renewing.
                var safeLockName = GenerateSafeLockName(name);
                var now = DateTimeOffset.UtcNow;
                var lockRecord = new LockRecord
                {
                    id = safeLockName,
                    name = name,
                    providerName = options.ProviderName,
                    lockObtainedAt = now,
                    lockLastRenewedAt = now,
                    _ttl = options.TTL
                };
                return await container.CreateItemAsync(lockRecord, new PartitionKey(lockRecord.id)).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Lock already held by someone else.
                return null;
            }
        }

        public async Task<ItemResponse<LockRecord>?> RenewLockAsync(ItemResponse<LockRecord> item)
        {
            try
            {
                var existing = item.Resource;
                var lockRecord = new LockRecord
                {
                    id = existing.id,
                    name = existing.name,
                    providerName = existing.providerName,
                    lockObtainedAt = existing.lockObtainedAt,
                    lockLastRenewedAt = DateTimeOffset.UtcNow,
                    _ttl = existing._ttl
                };
                return await container.ReplaceItemAsync(lockRecord, lockRecord.id, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag }).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Someone else already acquired a new lock, which means our lock was already released.
                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Lock record already expired via TTL.
                return null;
            }
        }

        public async Task ReleaseLockAsync(ItemResponse<LockRecord> item)
        {
            try
            {
                var lockRecord = item.Resource;
                _ = await container.DeleteItemAsync<LockRecord>(lockRecord.id, new PartitionKey(lockRecord.id), new ItemRequestOptions { IfMatchEtag = item.ETag }).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Someone else already acquired a new lock, which means our lock was already released.
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Lock record already expired via TTL.
            }
        }

        private static string GenerateSafeLockName(string lockName)
        {
            // '/', '\\', '?', '#' are invalid in a Cosmos DB id.
            return lockName.Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('#', '_');
        }
    }
}
