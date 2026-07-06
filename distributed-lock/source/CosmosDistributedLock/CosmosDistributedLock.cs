using Microsoft.Azure.Cosmos;

namespace Cosmos.DistributedLock
{
    /// <summary>
    /// Represents an attempt to acquire a distributed lock. When <see cref="IsAcquired"/> is
    /// true the lock is held, and a background keep-alive loop renews it until this object is
    /// disposed. Dispose releases the lock synchronously (deterministically).
    /// </summary>
    public class CosmosDistributedLock : IDisposable
    {
        private readonly TimeSpan keepAliveBuffer = TimeSpan.FromSeconds(1); // 1 second is the smallest Cosmos TTL increment
        private readonly ICosmosLockClient? cosmosLockClient;
        private volatile ItemResponse<LockRecord>? latestItem;
        private readonly string? lockId;
        private readonly long fencingToken;
        private readonly CancellationTokenSource? cts;
        private readonly Task? keepAliveTask;
        private int disposed;

        /// <summary>Raised each time the lock's lease is successfully renewed.</summary>
        public event Action? Renewed;

        internal static CosmosDistributedLock CreateUnacquiredLock()
        {
            return new CosmosDistributedLock();
        }

        internal static CosmosDistributedLock CreateAcquiredLock(ICosmosLockClient cosmosLockClient, ItemResponse<LockRecord> item, bool autoRenew = true)
        {
            return new CosmosDistributedLock(cosmosLockClient, item, autoRenew);
        }

        private CosmosDistributedLock()
        {
        }

        private CosmosDistributedLock(ICosmosLockClient cosmosLockClient, ItemResponse<LockRecord> item, bool autoRenew)
        {
            this.cosmosLockClient = cosmosLockClient;
            latestItem = item;
            fencingToken = SessionTokenParser.Parse(item.Headers.Session);
            lockId = $"{item.Resource.providerName}:{item.Resource.id}:{fencingToken}:{item.Resource.lockObtainedAt.Ticks}";
            cts = new CancellationTokenSource();

            // Production callers use automatic keep-alive. The distributed-lock web front end
            // disables it so it can drive/observe renewal itself (and demonstrate the failure
            // mode when a lock is NOT renewed).
            if (autoRenew)
            {
                keepAliveTask = KeepAliveLoop(item, cts.Token);
            }
        }

        /// <summary>True when this attempt acquired the lock.</summary>
        public bool IsAcquired => latestItem != null;

        /// <summary>A unique identifier for this specific acquisition of the lock.</summary>
        public string? LockId => lockId;

        /// <summary>A monotonically increasing fencing token (derived from the Cosmos session LSN).</summary>
        public long FencingToken => fencingToken;

        public string? ETag => latestItem?.ETag;

        /// <summary>When the lock's lease was last renewed (useful for a TTL countdown).</summary>
        public DateTimeOffset? LockLastRenewedAt => latestItem?.Resource.lockLastRenewedAt;

        /// <summary>The lock's time-to-live, in seconds.</summary>
        public int Ttl => latestItem?.Resource._ttl ?? 0;

        /// <summary>
        /// Manually renews the lock's lease (resetting its TTL). Returns <c>false</c> if the lock
        /// was lost — for example it already expired via TTL or was taken over by someone else.
        /// Useful when the lock was acquired with automatic keep-alive disabled.
        /// </summary>
        public async Task<bool> TryRenewAsync()
        {
            var current = latestItem;
            if (cosmosLockClient == null || current == null) return false;

            var updated = await cosmosLockClient.RenewLockAsync(current).ConfigureAwait(false);
            if (updated == null)
            {
                latestItem = null; // lock lost
                return false;
            }

            latestItem = updated;
            Renewed?.Invoke();
            return true;
        }

        private async Task KeepAliveLoop(ItemResponse<LockRecord> item, CancellationToken cancellationToken)
        {
            var current = item;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var lockRecord = current.Resource;
                    var lockExpiresAt = lockRecord!.lockLastRenewedAt + TimeSpan.FromSeconds(lockRecord._ttl);
                    var dueIn = lockExpiresAt - DateTimeOffset.UtcNow - keepAliveBuffer;

                    if (dueIn > TimeSpan.Zero)
                    {
                        await Task.Delay(dueIn, cancellationToken).ConfigureAwait(false);
                    }

                    var updatedItem = await cosmosLockClient!.RenewLockAsync(current).ConfigureAwait(false);
                    if (updatedItem == null) return;

                    current = updatedItem;
                    latestItem = updatedItem;
                    Renewed?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    // Dispose was called, signaling the keep-alive loop to stop; the lock is released after this exits.
                    return;
                }
                catch
                {
                    // Swallow to prevent unobserved task exceptions; the lock will expire via TTL.
                    return;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (cts == null || Interlocked.Exchange(ref disposed, 1) != 0) return;
                cts.Cancel();
                keepAliveTask?.GetAwaiter().GetResult();
                cts.Dispose();
                ReleaseLock();
            }
        }

        private void ReleaseLock()
        {
            var item = latestItem;
            if (cosmosLockClient == null || item == null) return;

            // Release synchronously so the lock release/disposal is deterministic.
            cosmosLockClient.ReleaseLockAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
