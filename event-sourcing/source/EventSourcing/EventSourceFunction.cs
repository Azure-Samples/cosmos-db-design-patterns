using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;



namespace EventSourcing
{
    public class EventSourceFunction
    {
        private readonly ILogger<EventSourceFunction> _logger;
        private readonly string _connectionString;
        private CosmosClient _cosmosClient;
        private Container _container;


        public EventSourceFunction(ILogger<EventSourceFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration["CosmosDBConnection"];

        }

        [Function("EventSourceFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            
            _logger.LogInformation("HTTP trigger function processed a new Cart Event.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string responseMessage;

            if (requestBody != null)
            {
                CartEvent cartEvent = JsonConvert.DeserializeObject<CartEvent>(requestBody) ?? throw new ArgumentException("Request body is empty");
                _logger.LogInformation(JsonConvert.SerializeObject(cartEvent, Formatting.Indented));

                _cosmosClient = new CosmosClient(_connectionString);
                Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync("EventSourcingDB");
                _container = await database.CreateContainerIfNotExistsAsync("CartEvents", "/CartId");

                await _container.CreateItemAsync(cartEvent, new PartitionKey(cartEvent.CartId));

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
