using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;

namespace Bucketing
{
    public static class CosmosPatternsBucketingExample
    {
        [FunctionName("CosmosPatternsBucketingExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string? deviceCount = req.Query["deviceCount"];
            string? timeout = req.Query["timeout"];

            Database db;

            Container container;

            string partitionKeyPath = "/DeviceId";
            
            DateTime TruncateToMinute(DateTime time) 
            {
                return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);
            }

            void Main(string? deviceCount, string? timeout)
            {
                List<SensorEvent> sensorEvents = new List<SensorEvent>();
                
                string databaseName ="Hotels";
                string containerName = "SensorEvents";
                
                CosmosClient client = new(
                    accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                    authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);

                db = client.CreateDatabaseIfNotExistsAsync(databaseName).Result;
                container = db.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath, throughput: 400).Result;

                int.TryParse(deviceCount, out int deviceCountInt);
                int.TryParse(timeout, out int timeoutInt);

                var time = DateTime.UtcNow;
                var currentTimeMinute = TruncateToMinute(time);
                var endTime = currentTimeMinute.AddMinutes(timeoutInt);
                var nextPublishTime = TruncateToMinute(time);

                while (currentTimeMinute <= endTime)
                {
                    // Sleep then increment time
                    System.Threading.Thread.Sleep(1000);
                    time = DateTime.UtcNow;
                    currentTimeMinute = TruncateToMinute(time);

                    // Only generate events on 5 second interval
                    if (time.Second % 5 == 0)
                    {
                        // System.Console.WriteLine(time.Second);
                        var events = SensorEvent.GenerateSensorEvents(deviceCountInt);
                        events.ForEach(e => sensorEvents.Add(e));
                    
                        // Only publish at 1 minute interval (seconds = 00)
                        if (currentTimeMinute > nextPublishTime) {
                            System.Console.WriteLine($"Calculating batch for {nextPublishTime.ToString()}");
                            var aggTimestamp = DateTime.UtcNow.ToString();
                            var summaryEvents = sensorEvents.GroupBy(e => e.DeviceId)
                            .Select(e => new SummarySensorEvent
                            {
                                DeviceId = e.Key,
                                eventTimestamp = nextPublishTime.ToString(),
                                numberOfReadings = e.Count(),
                                avgTemperature = e.Average(ea => ea.Temperature),
                                minTemperature = e.Min(ee => ee.Temperature),
                                maxTemperature = e.Max(ee => ee.Temperature),
                                readings = e.Select(ee => new Reading
                                    {
                                        eventTimestamp = ee.EventTimestamp,
                                        temperature = ee.Temperature
                                    }).ToArray(),
                                receivedTimestamp = aggTimestamp
                            }).ToList();
                            
                            summaryEvents.ForEach(row => System.Console.WriteLine($"{row.DeviceId}, {row.numberOfReadings}, {row.eventTimestamp}"));
                            summaryEvents.ForEach(row => container.CreateItemAsync(row, new PartitionKey(row.DeviceId)).Wait());

                            nextPublishTime = currentTimeMinute;
                            sensorEvents = new List<SensorEvent>();
                        }
                    }
                }

            }
        
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody) ?? throw new ArgumentException("Request body is empty");
            deviceCount = deviceCount ?? data?.deviceCount;
            timeout = timeout ?? data?.timeout;

            Main(deviceCount, timeout);

            string responseMessage = string.IsNullOrEmpty(deviceCount)
                ? "This HTTP triggered function requires a deviceCount parameter. Pass a deviceCount in the query string or in the request body to generate device events and write bucket results."
                : $"This HTTP triggered function executed successfully for {deviceCount} devices.";

            return new OkObjectResult(responseMessage);
        }
    }
}
