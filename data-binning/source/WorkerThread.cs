using DataBinning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;

namespace Cosmos_data_binning
{
    // Delegate that defines the signature for the callback method.
    public delegate void PostMessageCallback(string msg);

    internal class WorkerThread
    {
        int timeout;
        Container container;
        string deviceId;
        PostMessageCallback postMessage;

        public WorkerThread(int timeout, Container container, string deviceId, PostMessageCallback postMessage) 
        {
            this.timeout = timeout;
            this.container = container;
            this.deviceId = deviceId;
            this.postMessage = postMessage;
        }
                

        public async void SimulateEvents()
        {
            List<SensorEvent> sensorEvents = new List<SensorEvent>();

            var time = DateTime.UtcNow;

            var endTime = time.AddMinutes(timeout);
            var nextPublishTime = Utility.GetNextPublishTime(time);
            postMessage($"Device#{deviceId}: Current Time is {time}, next batch publishing at {nextPublishTime.ToString()}");
            
            while (true)
            {
                // Sleep then increment time,  
                System.Threading.Thread.Sleep(1000);
                time = DateTime.UtcNow;

                //Only generate events when seconds is a multiple of 5
                if (time.Second % 5 == 0)
                {

                    var sensorEvent = SensorEvent.GenerateSensorEvent(deviceId);
                    sensorEvents.Add(sensorEvent);

                    // Only publish at 1 minute interval (seconds = 00)
                    if (time > nextPublishTime)
                    {
                        SummarySensorEvent eventSummary=new SummarySensorEvent
                        {
                            DeviceId = deviceId,
                            eventTimestamp = nextPublishTime.ToString(),
                            numberOfReadings = sensorEvents.Count(),
                            avgTemperature = sensorEvents.Average(ea => ea.Temperature),
                            minTemperature = sensorEvents.Min(ee => ee.Temperature),
                            maxTemperature = sensorEvents.Max(ee => ee.Temperature),
                            readings = sensorEvents.Select(ee => new Reading
                            {
                                eventTimestamp = ee.EventTimestamp,
                                temperature = ee.Temperature
                            }).ToArray(),
                            receivedTimestamp = DateTime.UtcNow.ToString()
                        };                                                

                        await container.CreateItemAsync(eventSummary, new PartitionKey(eventSummary.DeviceId));

                        nextPublishTime = Utility.GetNextPublishTime(time);
                        postMessage($"Device#{deviceId}: Current Time is {time}, next batch publishing at {nextPublishTime.ToString()}");


                        sensorEvents = new List<SensorEvent>();

                        if (time > endTime)
                            return;
                    };
                }
            }
        
        }
    }
}
