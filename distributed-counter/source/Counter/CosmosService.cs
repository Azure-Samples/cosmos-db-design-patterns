using Microsoft.Azure.Cosmos;
using System.Net;
using CosmosDistributedCounter;
using System.ComponentModel;
using Microsoft.Azure.Cosmos.Linq;
using Newtonsoft.Json.Linq;

namespace CosmosDistributedLock.Services
{

    public class CosmosService
    {
        private readonly CosmosClient client;
        private readonly Database db;
        private readonly Microsoft.Azure.Cosmos.Container container;


        public CosmosService(string CosmosUri, string CosmosKey, string CosmosDatabase, string CosmosContainer)
        {

            client = new(
                accountEndpoint: CosmosUri,
                authKeyOrResourceToken: CosmosKey);

            db = client.GetDatabase(CosmosDatabase);

            container = db.GetContainer(CosmosContainer);
        }


        public async Task<PrimaryCounter> CreatePrimaryCounterAsync(string counterId, string counterName, long initialValue)
        {

            PrimaryCounter pc = new PrimaryCounter(counterId,counterName,initialValue);

            return await container.UpsertItemAsync(pc, new PartitionKey(counterName));

        }


        public async Task<PrimaryCounter> ReadPrimaryCounterAsync(string counterId)
        {

            try
            {
                return await container.ReadItemAsync<PrimaryCounter>(id: counterId, partitionKey: new PartitionKey(counterId));
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw new Exception("Error getting lock");
                }
            }
        }


        public async Task<DistributedCounter> CreateDistributedCounterAsync(string parentCounterId, long value)
        {

            DistributedCounter dc = new DistributedCounter(value, parentCounterId) ;

            try
            {
                return await container.CreateItemAsync(dc, new PartitionKey(parentCounterId));
            }
            catch (CosmosException ex)
            {
                Console.WriteLine(ex);
                return null;
            }

        }

        public async Task<PrimaryCounter> UpdatedPrimaryCounterStatusAsync(PrimaryCounter pc, CounterStatus status)
        {


            try
            {

                List<PatchOperation> operations = new()
                {
                    PatchOperation.Set($"/status", (int)status )
                };

                return await container.PatchItemAsync<PrimaryCounter>(pc.Id, new PartitionKey(pc.Id), patchOperations: operations, requestOptions: new PatchItemRequestOptions { FilterPredicate = "FROM Counters c where c.status !=" + (int)status});

                }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //some other error 
                    throw new Exception("Precondition failed while updating counter");

                }
                else
                {   //some other error 
                    throw new Exception("Error while updating counter");
                }
            }

        }


        public async Task<DistributedCounter> ReadDistributedCounterAsync(string counterId, string pk)
        {

            DistributedCounter dc;

            try
            {
                dc = await container.ReadItemAsync<DistributedCounter>(id: counterId, new PartitionKey(pk));

            }
            catch (CosmosException ce)
            {
                //There's no lease for this owner, swallow exception, return falise
                if (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    dc = null;
                }
                else //some other exception
                {
                    throw new Exception("Error getting lease");
                }
            }

            return dc;

        }

        public enum SortingOrder : int
        {
            SortAscending = 0,
            SortDescending = 1,
            NoSort = 2,
        }
        public async Task<List<DistributedCounter>> GetDistributedCountersAsync(string counterId, bool onlyActive=true,  SortingOrder sorting=SortingOrder.NoSort)
        {
            QueryDefinition query;

            string q = "SELECT * FROM c WHERE c.pk = @CounterId AND c.docType = @Type";
            if (onlyActive)
            {
                q = q + " AND c.status= @Status";
            }
            
            switch(sorting)
            {
                case SortingOrder.SortDescending:
                    q = q + " order by c.countervalue desc";
                    break;
                case SortingOrder.SortAscending:
                    q = q + " order by c.countervalue asc";
                    break;
            }

            if (onlyActive)
            {               
                query = new QueryDefinition(q)
                    .WithParameter("@CounterId", counterId)
                    .WithParameter("@Type", "DistributedCounter")
                    .WithParameter("@Status", CounterStatus.active);
            }
            else
            {
                query = new QueryDefinition(q)
                   .WithParameter("@CounterId", counterId)
                   .WithParameter("@Type", "DistributedCounter");
            }


            FeedIterator<DistributedCounter> results = container.GetItemQueryIterator<DistributedCounter>(query);

            List<DistributedCounter> dcList = new List<DistributedCounter>();

            try
            {
                while (results.HasMoreResults)
                {
                    FeedResponse<DistributedCounter> response = await results.ReadNextAsync();
                    foreach (DistributedCounter dc in response)
                    {
                        dcList.Add(dc);
                    }
                }

                return dcList;
            }
            catch(CosmosException ex)
            {
                return null;
            }

        }


        public async Task<DistributedCounter> UpdatedDistributedCounterStatusAsync(DistributedCounter dc, CounterStatus status)
        {


            try
            {

                List<PatchOperation> operations = new()
                {
                    PatchOperation.Set($"/status", (int)status )
                };

                return await container.PatchItemAsync<DistributedCounter>(dc.Id, new PartitionKey(dc.ParentCounter), patchOperations: operations, requestOptions: new PatchItemRequestOptions { FilterPredicate = "FROM Counters c WHERE c.status !=" + (int)status });

            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //some other error 
                    throw new Exception("Precondition failed while updating counter");

                }
                else
                {   //some other error 
                    throw new Exception("Error while updating counter");
                }
            }

        }


        public async Task<DistributedCounter> UpdateDistributedCounterValueandStatusAsync(DistributedCounter dc, CounterStatus status, long value)
        {


            try
            {

                List<PatchOperation> operations = new()
                {
                    PatchOperation.Set($"/status", status ),
                    PatchOperation.Set($"/countervalue",value)
                };

                return await container.PatchItemAsync<DistributedCounter>(dc.Id, new PartitionKey(dc.ParentCounter), patchOperations: operations, requestOptions: new PatchItemRequestOptions { IfMatchEtag = dc.ETag });

            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //some other error 
                    throw new Exception("Precondition failed while updating counter");

                }
                else
                {   //some other error 
                    throw new Exception("Error while updating counter");
                }
            }

        }

        public async Task<DistributedCounter> DecrementActiveDistributedCounterValueAsync(string pcId, string dcId, long value)
        {

            List<PatchOperation> operations = new()
            {
                PatchOperation.Increment ($"/countervalue",(value *-1))
            };

            return await container.PatchItemAsync<DistributedCounter>(dcId, new PartitionKey(pcId), patchOperations: operations, requestOptions: new PatchItemRequestOptions { FilterPredicate = "FROM Counters c WHERE c.status = 0 and c.countervalue >=" + value });

        }

    }
}
