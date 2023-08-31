using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Versioning
{
    public static class DocumentVersioningProcessor
    {
        [FunctionName("DocumentVersioningProcessor")]
        public static async Task Run([CosmosDBTrigger(
            databaseName: "Orders",
            containerName: "CurrentOrderStatus",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases", CreateLeaseContainerIfNotExists=true)]IReadOnlyList<VersionedOrder> input,
            [CosmosDB(databaseName: "Orders",
                        containerName: "HistoricalOrderStatus", 
                        Connection="CosmosDBConnection", CreateIfNotExists=true, PartitionKey="/CustomerId")] IAsyncCollector<VersionedOrder> historicalOrdersOut,
            ILogger log)
        {           
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Document count: " + input.Count);
                foreach (VersionedOrder versionedOrder in input){                    
                    log.LogInformation($"Processing {versionedOrder.OrderId} - Status: {versionedOrder.Status}");
                    // new id for the historical collection to preserve the history rather than overwrite it
                    versionedOrder.id = System.Guid.NewGuid().ToString();               
                    await historicalOrdersOut.AddAsync(versionedOrder);
                }                
            }
        }
    }
}