using Cosmos_Patterns_GlobalLock;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
using Microsoft.Extensions.Configuration;

namespace CosmosDistributedLock.Services
{
    public record LeaseRequestStatus
      (
        long fenceToken,
        string currentOwner
      );

    public class DistributedLockService 
    {

        private readonly CosmosService cosmos;
        private readonly int retryInterval;

        public DistributedLockService(IConfiguration configuration)
        {

            cosmos = new CosmosService(configuration);
            retryInterval = Convert.ToInt32(configuration["retryInterval"]);           

        }

        public async Task InitDatabaseAsync()
        {
            await cosmos.InitDatabaseAsync();
        }

        public async Task Init(string lockName)
        {
            // warm up SDK
            var distributedLock = await cosmos.ReadLockAsync(lockName);
        }
        
        public async Task<LeaseRequestStatus> AcquireLeaseAsync(string lockName, string newOwnerId, int leaseDuration, long existingFenceToken)
        {

            DistributedLock distributedLock;
            DistributedLock updatedDistributedLock;
            long newFenceToken;

    
            // #1: Find the lock
            distributedLock = // #1: Find the lock
            distributedLock = await cosmos.ReadLockAsync(lockName);

            if (distributedLock == null)
            {

                // #2: Lock doesn't exist. Create a new lease and lock. All Done.
                await CreateUpdateLeaseAsync(newOwnerId, leaseDuration);
                                
                newFenceToken = await CreateNewLockAsync(lockName, newOwnerId);

                if (newFenceToken != -1)
                {
                    //Return the fence token for the new lock with new lease //can maybe remove this after all conditions
                    return new LeaseRequestStatus(newFenceToken, newOwnerId);

                }
                else
                {
                    //return blank fence token    
                    return new LeaseRequestStatus(-1, "");
                }
            }
            else
            {
                //#3. Found the lock. Is this the owner or no owner?
                if (newOwnerId == distributedLock.OwnerId || string.IsNullOrEmpty(distributedLock.OwnerId))
                {
                    //Is owner, renew the lease. (Does using upsert matter? Maybe reduces round trips if Lease has expired?)
                    await CreateUpdateLeaseAsync(newOwnerId, leaseDuration);

                    
                    //update the fencetoken...

                    updatedDistributedLock = await AcquireLockAsync(distributedLock, newOwnerId);
                    newFenceToken = updatedDistributedLock.FenceToken;

                    return new LeaseRequestStatus(newFenceToken, updatedDistributedLock.OwnerId);
                    
                }
                else if(!string.IsNullOrEmpty(distributedLock.OwnerId))
                {

                    //#4. Not the owner. See if there is a valid Lease for this owner
                    bool isValidLease = await IsValidLeaseAsync(distributedLock.OwnerId);

                    if (!isValidLease)
                    {                            // #5. No Valid Lease by current listed owner.

                            //Create a new lease for owner
                            await CreateUpdateLeaseAsync(newOwnerId, leaseDuration);

                        //Take the lock, Return the new fence token for the lock with new lease
                         updatedDistributedLock = await AcquireLockAsync(distributedLock, newOwnerId);
                        newFenceToken = updatedDistributedLock.FenceToken;

                        return new LeaseRequestStatus(newFenceToken, updatedDistributedLock.OwnerId);
                    }
                    else
                    {
                        //return latest fence token  and owner
                        updatedDistributedLock = await ReadLockAsync(distributedLock.LockName);
                        newFenceToken = updatedDistributedLock.FenceToken;

                        return new LeaseRequestStatus(newFenceToken, updatedDistributedLock.OwnerId);
                    }
                }

            }

            //return blank fence token, should never come here    
            return new LeaseRequestStatus(-1, "");

        }

        public async Task<bool> ValidateLeaseAsync(string lockName, string ownerId, long fenceToken)
        {
            DistributedLock distributedLock;

            //Find the lock
            distributedLock = await cosmos.ReadLockAsync(lockName);

            //Lock doesn't exista

            if(distributedLock == null)
                return false;

            //newer fence token later avilable in lock
            if (distributedLock.FenceToken > fenceToken)
                return false;

            //Valid lease for Lock, with valid owner and fence token
            if (distributedLock.OwnerId == ownerId && distributedLock.FenceToken == fenceToken)
            {
                //still have a valid lease?
                Lease lease = await cosmos.ReadLeaseAsync(ownerId);

                if (lease == null)
                {
                    //remove the current owner from lock
                    distributedLock.OwnerId = "";
                    //distributedLock.FenceToken += 1;
                    await cosmos.UpdateLockAsync(distributedLock);
                    return false;
                }

                return true;
            }

            return false;
        }
        
        //public async Task ReleaseLeaseAsync(string ownerId)
        //{
        //    await ReleaseLeaseAsync(ownerId);
        //}        

        private async Task<long> CreateNewLockAsync(string lockName, string ownerId)
        {

            long newFenceToken = await cosmos.CreateNewLockAsync(lockName, ownerId);

            return newFenceToken;
        }

        private async Task<DistributedLock> AcquireLockAsync(DistributedLock distributedLock, string newOwnerId) 
        {
            distributedLock.OwnerId = newOwnerId;
            //distributedLock.FenceToken += 1;

            return await cosmos.UpdateLockAsync(distributedLock);
        }

        private async Task ReleaseLockAsync(DistributedLock distributedLock)
        {
            // Set owner to empty string to release ownership of the lock
            distributedLock.OwnerId = "";
            await cosmos.UpdateLockAsync(distributedLock);
        }

        private async Task CreateUpdateLeaseAsync(string ownerId, int leaseDuration)
        {

            var lease = await cosmos.CreateUpdateLeaseAsync(ownerId, leaseDuration);

        }

        private async Task<bool> IsValidLeaseAsync(string ownerId)
        {
            Lease lease = await cosmos.ReadLeaseAsync(ownerId);
            if (lease != null) { return true; }
            return false;
        }

        public async Task ReleaseLeaseAsync(string ownerId)
        {
            await cosmos.DeleteLeaseAsync(ownerId);
        }

        private async Task<DistributedLock> ReadLockAsync(string lockName)
        {
            return await cosmos.ReadLockAsync(lockName);
        }
    }
}
