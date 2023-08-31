// See https://aka.ms/new-console-template for more information
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using Newtonsoft.Json;

namespace Bucketing
{
    internal class Program 
    {
        static string urlBase = "http://localhost:7071"; // "http://<functionapp>.azurewebsites.net:7071"
 
        public static async Task<string> SimulateEvents(int deviceCount, int timeout)
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

            var url = $"{urlBase}/api/CosmosPatternsBucketingExample";
            var args = new Dictionary<string, int>
            {
                {"deviceCount", deviceCount},
                {"timeout", timeout}
            };
            string jsonBody = JsonConvert.SerializeObject(args);
            var body = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, body);
            string result = await response.Content.ReadAsStringAsync();
            return result;
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


            var simulateEventsResult = await SimulateEvents(deviceCount, timeout);
            
            System.Console.WriteLine($"Function completed generation events for {deviceCountInput} devices");
            Console.WriteLine($"Check SensorEventContainer for new sensor events");
        }
    }
}
