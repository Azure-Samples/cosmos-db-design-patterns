using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cosmos_Patterns_EventSourcing
{
    public static class CosmosPatternsEventSourcingExample
    {
        [FunctionName("CosmosPatternsEventSourcingExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [CosmosDB(databaseName: "Sales",
                        containerName: "CartEvents", 
                        Connection="CosmosDBConnection", CreateIfNotExists=true, PartitionKey="/CartId")] IAsyncCollector<CartEvent> cartEventOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string responseMessage;
            
            if (requestBody != null)
            {
                CartEvent cartEvent = JsonConvert.DeserializeObject<CartEvent>(requestBody) ?? throw new ArgumentException("Request body is empty");
                await cartEventOut.AddAsync(cartEvent);

                responseMessage = $"HTTP function successful for event {cartEvent.EventType} for cart {cartEvent.CartId}.";
            } else {
                responseMessage = "No event sent in body";
            }
            
            return new OkObjectResult(responseMessage);
        }
    }
}
