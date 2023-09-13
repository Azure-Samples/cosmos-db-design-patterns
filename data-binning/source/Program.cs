// See https://aka.ms/new-console-template for more information
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using static Azure.Core.HttpHeader;
using Microsoft.Extensions.Configuration;
using Container = Microsoft.Azure.Cosmos.Container;
using Database = Microsoft.Azure.Cosmos.Database;

namespace data_binning
{
    internal class Program 
    {



        static DateTime TruncateToMinute(DateTime time)
        {
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);
        }

        public static async Task SimulateEvents(int deviceCount, int timeout)
        {
            Database db;
            Container container;
            string partitionKeyPath = "/DeviceId";

            var configuration = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                 .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

            var config = configuration.Build();


            string uri = config["CosmosUri"];
            string key = config["CosmosKey"];

            CosmosClient client = new(
                accountEndpoint: uri!,
                authKeyOrResourceToken: key!);


            string databaseName = "Hotels";
            string containerName = "SensorEvents";

            db = client.CreateDatabaseIfNotExistsAsync(databaseName).Result;
            container = db.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: partitionKeyPath, throughput: 400).Result;

            List<SensorEvent> sensorEvents = new List<SensorEvent>();

            var time = DateTime.UtcNow;
            var currentTimeMinute = TruncateToMinute(time);
            var endTime = currentTimeMinute.AddMinutes(timeout);
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
                    var events = SensorEvent.GenerateSensorEvents(deviceCount);
                    events.ForEach(e => sensorEvents.Add(e));

                    // Only publish at 1 minute interval (seconds = 00)
                    if (currentTimeMinute > nextPublishTime)
                    {
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

        static async Task Main(string[] args)
        {
            List<SensorEvent> sensorEvents = new List<SensorEvent>();

            Console.WriteLine("This code will generate sensor events and bucket them by minute before saving to an Azure Cosmos DB for NoSQL account.");

            Console.WriteLine("How many devices would you like to generate data for (enter number between 1 and 100)?");
            var deviceCountInput = Console.ReadLine();
            
            Console.WriteLine("How many minutes would you like to generate data for (enter number between 1 and 10)?");
            var timeoutInput = Console.ReadLine();

            int.TryParse(deviceCountInput, out int deviceCount);
            int.TryParse(timeoutInput, out int timeout);

            Console.WriteLine($"Please wait while events are simulated for {timeoutInput} minutes.");

            await SimulateEvents(deviceCount, timeout);
            
            System.Console.WriteLine($"Function completed generation events for {deviceCountInput} devices");
            Console.WriteLine($"Check SensorEventContainer for new sensor events");
        }
    }
}
