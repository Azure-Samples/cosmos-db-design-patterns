using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EventSourcing
{
    public class EventSourceFunction
    {
        private readonly ILogger _logger;

        public EventSourceFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EventSourceFunction>();
        }

        [Function("EventSourcing")]
        [CosmosDBOutput(
                databaseName: "EventSourcingDB",
                containerName: "CartEvents",
                Connection = "CosmosDBConnection",
                CreateIfNotExists = true,
                PartitionKey = "/CartId")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
                IAsyncCollector<CartEvent> cartEventOut,
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
            }
            else
            {
                responseMessage = "No event sent in body";
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
