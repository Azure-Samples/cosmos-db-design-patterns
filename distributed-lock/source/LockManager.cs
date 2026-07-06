using CosmosDistributedLock.Services;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Net;

namespace Cosmos_Patterns_GlobalLock
{
    /// <summary>
    /// This represents a lock in the Cosmos DB.  Also used as the target of the lock.
    /// </summary>
    public class DistributedLock 
    {
        [JsonProperty("id")]
        public string LockName { get; set; } //Lock Name

        [JsonProperty("_etag")]
        public string ETag { get; set; } //Can I update or has someone else taken the lock

        [JsonProperty("_ts")]
        public long Ts { get; set; }
        
        public string OwnerId { get; set; } //ownerId, ClientId

        public long FenceToken { get; set; } //Incrementing token

        [JsonProperty("ttl")]
        public int Ttl { get; set; } = -1; //Lock persists indefinitely; only the Lease expires (via its own ttl).
    }

    public class Lease
    {
        [JsonProperty("id")]
        public string OwnerId { get; set; } //ownerId, clientId

        [JsonProperty("ttl")]
        public int LeaseDuration { get; set; } //leaseDuration in seconds

        [JsonProperty("_ts")]
        public long Ts { get; set; }

    }

    public class LockManager : IDisposable
    {
        DistributedLockService dls;
        
        string lockName;
        public string ownerId;
        public string Name;

        public string leaseOwnerId;

        private readonly Action<string>? onLeaseRenewed;
        private int leaseDuration;
        private CancellationTokenSource? renewalCts;
        private Task? renewalTask;

        /// <summary>
        /// This creates a container that has the TTL feature enabled.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="lockDbName"></param>
        /// <param name="lockContainerName"></param>
        /// <param name="lockName"></param>
        /// <param name="refreshIntervalS"></param>
        public LockManager( DistributedLockService dls, string lockName, string threadName, Action<string>? onLeaseRenewed = null)
        {
            this.dls = dls;
            
            this.lockName = lockName;
            
            this.ownerId = Guid.NewGuid().ToString();

            this.Name = threadName;

            this.onLeaseRenewed = onLeaseRenewed;

        }

        /// <summary>
        /// Simple static constructor
        /// </summary>
        /// <param name="client"></param>
        /// <param name="lockDb"></param>
        /// <param name="lockContainer"></param>
        /// <param name="lockName"></param>
        /// <returns></returns>
        static public async Task<LockManager> CreateLockAsync(DistributedLockService dls, string lockName, string threadName, Action<string>? onLeaseRenewed = null)
        {
            return new LockManager( dls, lockName, threadName, onLeaseRenewed);
        }

        /// <summary>
        /// This function will check for a lease object (if it exists).  If it does, it checks to see if the current client has the lease.  If the lease is expired, it will automatically be deleted by Cosmos DB via the TTL property.
        /// </summary>
        /// <param name="leaseDurationS"></param>
        /// <returns></returns>
        public async Task<LeaseRequestStatus> AcquireLeaseAsync(int leaseDuration, long existingFenceToken)
        {
            try
            {                
                this.leaseDuration = leaseDuration;

                var reqStatus= await dls.AcquireLeaseAsync(lockName, ownerId, leaseDuration,existingFenceToken);
                leaseOwnerId = reqStatus.currentOwner;

                // If we acquired the lock, keep the lease alive by renewing it in the
                // background until the lock is released or disposed. This prevents the lease
                // from expiring (and another worker starting) while work is still in progress
                // and runs longer than the lease duration.
                if (reqStatus.currentOwner == ownerId && reqStatus.fenceToken > 0)
                {
                    StartRenewal();
                }

                return reqStatus;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<bool> ReleaseLeaseAsync()
        {
            StopRenewal();

            try
            {   
                if(leaseOwnerId== ownerId)
                    await dls.ReleaseLeaseAsync(ownerId);

                return true;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                throw;
            }
        }

        /// <summary>
        /// This function will check to see if the current token is valid.  It is possible that the lease has expired and a new lease needs to be created.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> HasLeaseAsync(long token)
        {
            try
            {
                return await dls.ValidateLeaseAsync(lockName, ownerId, token);
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                throw;
            }
        }



        private void StartRenewal()
        {
            renewalCts = new CancellationTokenSource();
            renewalTask = RenewLeaseLoopAsync(renewalCts.Token);
        }

        private async Task RenewLeaseLoopAsync(CancellationToken cancellationToken)
        {
            // Renew well before the lease expires (at least once per half of the duration).
            int renewIntervalMs = Math.Max(1, leaseDuration / 2) * 1000;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(renewIntervalMs, cancellationToken);
                    await dls.RenewLeaseAsync(ownerId, leaseDuration);
                    onLeaseRenewed?.Invoke($"{Name}: Renewed lease on lock [{lockName}] while work continues.");
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when the lock is released or disposed.
            }
        }

        private void StopRenewal()
        {
            if (renewalCts == null)
                return;

            renewalCts.Cancel();

            try
            {
                renewalTask?.Wait();
            }
            catch
            {
                // Ignore the cancellation exception surfaced by Wait().
            }

            renewalCts.Dispose();
            renewalCts = null;
            renewalTask = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRenewal();
                _ = ReleaseLeaseAsync();
            }

        }
    }

}
