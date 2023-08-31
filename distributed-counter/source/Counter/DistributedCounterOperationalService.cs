using CosmosDistributedCounter;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
using CosmosDistributedLock.Services;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Drawing.Text;
using System.Diagnostics.Metrics;
using System.Runtime.ConstrainedExecution;

namespace CosmosDistributedCounter
{

    public class DistributedCounterOperationalService
    {
        private readonly CosmosService cosmos;

        private List<DistributedCounter> dcCache = new List<DistributedCounter>();
        private DateTime dcCacheExpiryDateTime;

        const int MINVALUE_OF_DC_FOR_MERGE = 15;

        public DistributedCounterOperationalService(string CosmosUri, string CosmosKey, string CosmosDatabase, string CosmosContainer)
        {
            cosmos = new CosmosService(CosmosUri, CosmosKey, CosmosDatabase, CosmosContainer);
        }

        public async Task<PrimaryCounter> GetPrimaryCounterAsync(string counterId)
        {
            return await cosmos.ReadPrimaryCounterAsync(counterId);
        }

        public async Task<bool> DecrementDistributedCounterValueAsync(PrimaryCounter pc, long decrementValue)
        {
            string dcId=string.Empty;
            if (System.DateTime.Now> dcCacheExpiryDateTime)
            {
                dcCache = await cosmos.GetDistributedCountersAsync(pc.Id, true);

                if(dcCache==null)
                    return false;

                dcCacheExpiryDateTime = System.DateTime.Now.AddSeconds(30);
            }
            

            Random r = new Random();
            int rInt = r.Next(0, dcCache.Count); //pick a random DC to update

            dcId = dcCache[rInt].Id;
              

            try
            {
                await cosmos.DecrementActiveDistributedCounterValueAsync(pc.Id,dcId, decrementValue);
                return true;
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    DistributedCounter dc= await cosmos.ReadDistributedCounterAsync(dcId,pc.Id);
                    if (dc.Value < MINVALUE_OF_DC_FOR_MERGE)
                    {
                        MergeDistributedCountersWithLowValue(pc);//converge the dcList with  small values 
                    }
                    return false;
                }
                else
                {   //some other error 
                    throw new Exception("Error while updating counter");
                }
            }

            return false;
            
            
        }

        private async Task MergeDistributedCountersWithLowValue(PrimaryCounter pc)
        {
            List<DistributedCounter> dcList = await cosmos.GetDistributedCountersAsync(pc.Id, true, CosmosService.SortingOrder.SortAscending);
            
            if (dcList.Count ==1) //last dc remaining
            { return; }

            pc = await cosmos.ReadPrimaryCounterAsync(pc.Id);
            try
            {
                pc = await cosmos.UpdatedPrimaryCounterStatusAsync(pc, CounterStatus.updating);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    //pc is  busy, try  later;
                    return;
                }
            }

            //reduce the  values of  existing  dc
            int ctr = 1;
            bool mergeWithNext = false;
            long valueFromPrev = 0;


            for (int i = 0; i < dcList.Count; i++)
            {

                DistributedCounter dc = dcList[i];

                if (valueFromPrev < MINVALUE_OF_DC_FOR_MERGE) // merge if dc value is 15 or below
                {
                    
                    DistributedCounter dc_deleted=null;
                    if (mergeWithNext == false)
                    {
                        if (i < dcList.Count - 1) //ensure there is next item to merge
                        {
                           
                            //set status to deleted , so that no one updates it
                            try
                            {
                                DistributedCounter dc_merged;
                                dc_deleted = dc;
                                dc_merged = await cosmos.UpdatedDistributedCounterStatusAsync(dc, CounterStatus.deleted);
                                valueFromPrev = dc_merged.Value; //read value to merge with next
                                mergeWithNext = true; //next dc will merge with this
                            }
                            catch
                            {
                                //merge could not be  done, will skip for now
                            }
                        }

                    }
                    else
                    {
                        for(int j=0;j<5;j++) //try 5 times to merge the previously read value.
                        {
                            if (await MergeDC(dc, valueFromPrev) == true)
                                break;

                            if(j ==5)
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
