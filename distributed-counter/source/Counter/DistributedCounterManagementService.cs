using CosmosDistributedCounter;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
using CosmosDistributedLock.Services;
using System.Security.Cryptography;
using System.Diagnostics.Metrics;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace CosmosDistributedCounter
{
   
    public class DistributedCounterManagementService
    {

        private readonly CosmosService cosmos;

        public DistributedCounterManagementService(string CosmosUri, string CosmosKey, string CosmosDatabase, string CosmosContainer)
        {
            cosmos = new CosmosService(CosmosUri, CosmosKey, CosmosDatabase, CosmosContainer);     
        }               

        public async Task<PrimaryCounter> CreateCounterAsync( string counterName, long initialValue, int distributedCounters)
        {
            string counterId = Guid.NewGuid().ToString();

            int dcValue = (int) initialValue / distributedCounters;
           
            List<string> counterIds = new List<string>();

            for (int i = 1; i <= distributedCounters; i++)
            {
               if(i== distributedCounters) 
               {
                    dcValue = (int) (initialValue - ((i-1) * dcValue)); // to accomdate decimal error
               }

               DistributedCounter dc=  await cosmos.CreateDistributedCounterAsync(counterId, dcValue);
            }

            return await cosmos.CreatePrimaryCounterAsync(counterId, counterId, initialValue);

        }


        public async Task<PrimaryCounter> ActivateCountersAsync(PrimaryCounter pc)
        {
            return await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.active);
        }

        public async Task<PrimaryCounter> GetPrimaryCounterAsync(string counterId)
        {
            return await cosmos.ReadPrimaryCounterAsync(counterId);
        }

        public async Task<List<DistributedCounter>> GetDistributedCountersAsync(PrimaryCounter pc, bool onlyActive=true)
        {
            return await cosmos.GetDistributedCountersAsync(pc.Id, onlyActive);
        }
                
        public async Task<List<DistributedCounter>> SplitDistributedCountersAsync(PrimaryCounter pc, int count)
        {
           
            List<DistributedCounter> dcList = await cosmos.GetDistributedCountersAsync(pc.Id,true, CosmosService.SortingOrder.SortDescending);

            // update PC to updating
            pc = await GetPrimaryCounterAsync(pc.Id);

            try
            {
                pc = await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.updating);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //pc is  busy, try  later;
                    return null;
                }
            }

            //reduce the  values of  existing  dc
            int ctr = 1;
            foreach(DistributedCounter dc in dcList)
            {
                if ( ctr <= count)
                {
                    //set status to paused , so that no one updates it
                    DistributedCounter dc_updated=  await cosmos.UpdatedDistributedCounterStatusAsync(dc, CounterStatus.paused);

                    long orgValue = dc.Value;
                    long newValue = orgValue / 2;

                    //update DC to 50% value, status =active, back to rotation
                    await cosmos.UpdateDistributedCounterValueandStatusAsync(dc_updated, CounterStatus.active, newValue);

                    try
                    {
                        //create new DC with 50% value
                        DistributedCounter dc_new = await cosmos.CreateDistributedCounterAsync(pc.Id, orgValue - newValue);
                        ctr++;
                    }

                    catch (CosmosException ex)
                    {
                        //reverting  counter back to active
                        for (int j = 0; j < 5; j++) //try 5 times
                        {
                            if ( await cosmos.UpdateDistributedCounterValueandStatusAsync(dc_updated, CounterStatus.active, orgValue)!=null)
                                break;

                            if (j == 5)
                            {
                                throw new Exception("DC Split failed for " + dc_updated.Id);
                            }
                        }
                    }                   
                }                
            }

            await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.active);

            return await cosmos.GetDistributedCountersAsync(pc.Id, true, CosmosService.SortingOrder.SortDescending); 

        }

        public async Task<List<DistributedCounter>> MergeDistributedCountersAsync(PrimaryCounter pc, int count)
        {
            List<DistributedCounter> dcList = await cosmos.GetDistributedCountersAsync(pc.Id, true, CosmosService.SortingOrder.SortAscending);


            // update PC to updating
            pc = await GetPrimaryCounterAsync(pc.Id);
            try
            {
                pc = await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.updating);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //pc is  busy, try  later;
                    return null;
                }
            }

            //reduce the  values of  existing  dc
            int ctr = 1;
            bool mergeWithNext = false;
            long valueFromPrev = 0;
            for (int i = 0; i < dcList.Count; i++)
            {

                DistributedCounter dc = dcList[i];

                if (ctr <= count)
                {
                    DistributedCounter dc_deleted = null;
                    if (mergeWithNext == false)
                    {
                        if (i < dcList.Count - 1) //ensure there is next item to merge
                        {
                            DistributedCounter dc_merged;
                            //set status to deleted , so that no one updates it
                            try
                            {
                                dc_deleted = dc;
                                dc_merged = await cosmos.UpdatedDistributedCounterStatusAsync(dc, CounterStatus.deleted);

                                valueFromPrev = dc_merged.Value; //read value to merge with next

                                mergeWithNext = true; //next dc will merge with this
                            }
                            catch
                            {
                                //merge could not be  done
                                continue;
                            }
                        }

                    }
                    else
                    {
                        for (int j = 0; j < 5; j++) //try 5 times
                        {
                            if (await MergeDC(dc, valueFromPrev) == true)
                                break;

                            if (j == 5)
                            {
                                try
                                {
                                    dc_deleted = await cosmos.UpdatedDistributedCounterStatusAsync(dc_deleted, CounterStatus.active); //restore back to active
                                }
                                catch
                                {
                                    throw new Exception("DC Merge failed into " + dc.Id + " , source was " + dc_deleted.Id); //if merge failed and also restore
                                }
                            }
                        }
                        valueFromPrev = 0;
                        mergeWithNext = false;
                        ctr++;
                    }

                }
            }

            await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.active);

            return await cosmos.GetDistributedCountersAsync(pc.Id, true, CosmosService.SortingOrder.SortAscending);
        }

        private async Task<bool> MergeDC(DistributedCounter dc, long mergeValue)
        {
            try
            {
                DistributedCounter dc_updated = await cosmos.UpdatedDistributedCounterStatusAsync(dc, CounterStatus.paused);

                long orgValue = dc.Value;
                long newValue = orgValue + mergeValue;

                //update DC with new value, status =active, back to rotation
                await cosmos.UpdateDistributedCounterValueandStatusAsync(dc_updated, CounterStatus.active, newValue);

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
