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
        private readonly ILogger<EventSourceFunction> _logger;

        public EventSourceFunction(ILogger<EventSourceFunction> logger)
        {
            _logger = logger;
        }

        [Function("EventSourcing")]
        public async Task<EventSourcingOutput> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, 
                FunctionContext context)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                return new EventSourcingOutput
                {
                    HttpResponse = new OkObjectResult("No event sent in body")
                };
            }

            CartEvent cartEvent = JsonConvert.DeserializeObject<CartEvent>(requestBody) ?? throw new ArgumentException("Request body is empty");
            _logger.LogInformation(JsonConvert.SerializeObject(cartEvent, Formatting.Indented));

            return new EventSourcingOutput
            {
                CartEvent = cartEvent,
                HttpResponse = new OkObjectResult(cartEvent)
            };
        }
    }

    public class EventSourcingOutput
    {
        [CosmosDBOutput(
            databaseName: "EventSourcingDB",
            containerName: "CartEvents",
            Connection = "CosmosDBConnection",
            CreateIfNotExists = true,
            PartitionKey = "/CartId")]
        public CartEvent? CartEvent { get; set; }

        [HttpResult]
        public IActionResult HttpResponse { get; set; } = new OkResult();
    }
}
