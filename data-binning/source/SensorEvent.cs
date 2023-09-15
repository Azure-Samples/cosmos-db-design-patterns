using Newtonsoft.Json;
using System.Net;

namespace DataBinning
{
    public class SensorEvent
    {
        public string? DeviceId { get; set; }
        public double Temperature { get; set; }
        public string Unit { get; set; } = "Fahrenheit";
        public string EventTimestamp { get; set; } = DateTime.UtcNow.ToString();
        public string ReceivedTimestamp { get; set; } = DateTime.UtcNow.ToString();
    
        public static SensorEvent GenerateSensorEvent(string deviceId)
        {
           
            Random rng = new Random();
            
            var sessionId = Guid.NewGuid().ToString();

            // Calculate temperature between 65 and 79 (as double)
            double temperature = rng.NextDouble() * (79.0 - 65.0) + 65.0;

            var sensorEvent = new SensorEvent();
            sensorEvent.DeviceId = deviceId;
            sensorEvent.Temperature = temperature;

            return sensorEvent;
        }
    }

    public class Reading
    {
        public string? eventTimestamp { get; set; }
        public double temperature { get; set; }

    }
    public class SummarySensorEvent
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string? DeviceId { get; set; }
        public int numberOfReadings { get; set; }
        public double avgTemperature { get; set; }
        public double minTemperature { get; set; }
        public double maxTemperature { get; set; }
        public Reading[]? readings { get; set; }
        public string? eventTimestamp { get; set; }
        public string? receivedTimestamp { get; set; }
    }
}
