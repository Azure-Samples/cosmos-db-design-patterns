using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace MaterializedViews
{
    public static class MaterializedViewProcessor
    {
        [FunctionName("MaterializedViewProcessor")]
        public static async Task Run([CosmosDBTrigger(
            databaseName: "Sales",
            containerName: "Sales",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases", CreateLeaseContainerIfNotExists=true)]IReadOnlyList<Sales> input,
            [CosmosDB(databaseName: "Sales",
                        containerName: "SalesByProduct", 
                        Connection="CosmosDBConnection", CreateIfNotExists=true, PartitionKey="/Product")] IAsyncCollector<SalesByProduct> salesByProduct,
            ILogger log)
        {           
            if (input != null && input.Count > 0)
            {
                log.LogInformation("Document count: " + input.Count);
                foreach (Sales document in input){
                    await salesByProduct.AddAsync(new SalesByProduct(document));                                                
                }
            }
        }
    }
}
