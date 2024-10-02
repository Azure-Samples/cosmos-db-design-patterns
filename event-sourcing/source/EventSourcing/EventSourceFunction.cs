using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EventSourcing
{
    public class EventSourceFunction
    {
        private readonly ILogger<EventSourceFunction> _logger;

        public EventSourceFunction(ILogger<EventSourceFunction> logger)
        {
            _logger = logger;
        }

        [Function("EventSourceFunction")]
        [CosmosDBOutput(
                databaseName: "EventSourcingDB",
                containerName: "CartEvents",
                Connection = "CosmosDBConnection",
                CreateIfNotExists = true,
                PartitionKey = "/CartId")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            
            _logger.LogInformation("HTTP trigger function processed a new Cart Event.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string responseMessage;

            if (requestBody != null)
            {
                CartEvent cartEvent = JsonConvert.DeserializeObject<CartEvent>(requestBody) ?? throw new ArgumentException("Request body is empty");
                _logger.LogInformation(JsonConvert.SerializeObject(cartEvent, Formatting.Indented));

                responseMessage = $"HTTP function successful for event {cartEvent.EventType} for cart {cartEvent.CartId}.";
                return new OkObjectResult(cartEvent);
            }
            else
            {
                responseMessage = "No event sent in body";
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
