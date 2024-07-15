using Cosmos_Patterns_GlobalLock;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Net;
using Container = Microsoft.Azure.Cosmos.Container;

namespace CosmosDistributedLock.Services
{

    public class CosmosService
    {
        private Container? _container;
        private readonly IConfiguration _configuration;

        public CosmosService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task InitDatabaseAsync()
        {
            string uri = _configuration["CosmosUri"]!;
            string key = _configuration["CosmosKey"]!;

            string databaseName = _configuration["CosmosDatabase"]!;
            string containerName = _configuration["CosmosContainer"]!;

            CosmosClient client = new(
                accountEndpoint: uri,
                authKeyOrResourceToken: key);

            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            ContainerProperties containerProperties = new ()
            {
                Id = containerName,
                PartitionKeyPath = "/id",
                DefaultTimeToLive = 60  //seconds
            };
            ThroughputProperties throughputProperties = ThroughputProperties.CreateManualThroughput(400);
            
            _container = await database.CreateContainerIfNotExistsAsync(containerProperties, throughputProperties);
        }

        public async Task<Lease> CreateUpdateLeaseAsync(string ownerId, int leaseDuration)
        {

            Lease lease = new Lease { OwnerId = ownerId, LeaseDuration = leaseDuration };

            return await _container.UpsertItemAsync(lease, new PartitionKey(ownerId));

        }

        public async Task<Lease> ReadLeaseAsync(string ownerId)
        {

            Lease lease;

            try
            {
                lease = await _container.ReadItemAsync<Lease>(id: ownerId, new PartitionKey(ownerId));

            }
            catch (CosmosException ce)
            {
                //There's no lease for this owner, swallow exception, return falise
                if (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    lease = null;
                }
                else //some other exception
                {
                    throw new Exception("Error getting lease");
                }
            }

            return lease;

        }

        public async Task<DistributedLock> ReadLockAsync(string lockName)
        {
            DistributedLock returnLock = new();

            try
            {
                returnLock = await _container.ReadItemAsync<DistributedLock>(
                    id: lockName, 
                    partitionKey: new PartitionKey(lockName)
                );
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    returnLock = null;
                }
                else
                {
                    throw new Exception("Error getting lock");
                }
            }

            return returnLock;
        }

        public async Task<long> CreateNewLockAsync(string lockName, string ownerId)
        {

            //New Lock start with 1 for fence token to monotonically increment forever.
            long fenceToken = 1;

            DistributedLock newLock = new DistributedLock { LockName = lockName, OwnerId = ownerId, FenceToken = fenceToken };

            try
            {
                await _container.CreateItemAsync(newLock, new PartitionKey(newLock.LockName));
            }
            catch (CosmosException)
            {
                //swallow the exception and return -1 to indicate the new lock failed
                fenceToken = -1;
            }

            return fenceToken;

        }

        public async Task<DistributedLock> UpdateLockAsync(DistributedLock distributedLock)
        {
            DistributedLock updatedLock;

            try
            {
                // Take the lock
                List<PatchOperation> operations = new()
                {
                    PatchOperation.Set($"/OwnerId", distributedLock.OwnerId),
                    PatchOperation.Increment($"/FenceToken",1)
                };

                updatedLock = await _container.PatchItemAsync<DistributedLock>(
                    id: distributedLock.LockName, 
                    partitionKey: new PartitionKey(distributedLock.LockName), 
                    patchOperations: operations, 
                    requestOptions: new PatchItemRequestOptions { IfMatchEtag = distributedLock.ETag}
                );

                return updatedLock;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //Someone aleady got the lock. Swallow exception
                    return await ReadLockAsync(distributedLock.LockName);

                }
                else
                {   //some other error 
                    throw new Exception("Error updating Lock");
                }
            }

        }

        internal async Task DeleteLeaseAsync(string ownerId)
        {
            await _container.DeleteItemAsync<Lease>(id: ownerId, partitionKey: new PartitionKey(ownerId));
        }
    }
}
