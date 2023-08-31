using CosmosDistributedLock.Services;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.Net;


namespace CosmosDistributedCounter
{

    public enum CounterStatus : int
    {
        active = 0,
        deleted = 1,
        updating = 2,
        paused = 2,
        pending = 3
    }



    /// <summary>
    /// This represents a Counter in the Cosmos DB. Will be distributed into multiple distributed Counters.
    /// </summary>
    public class PrimaryCounter
    {
        [JsonProperty("pk")]
        public string PK { get; set; } //Counter Name, also the PK

        [JsonProperty("id")]
        public string Id { get; set; } //Id will be same as PK

        [JsonProperty("name")]
        public string CounterName { get; set; } //Id will be same as PK

        [JsonProperty("_etag")]
        public string ETag { get; set; } //Can I update or has someone else taken the lock

        [JsonProperty("_ts")]
        public long Ts { get; set; }

        [JsonProperty("startvalue")]
        public long Counter_StartValue { get; set; } //Start value of the Counter

        [JsonProperty("status")]
        public CounterStatus Status { get; set; } // status of  the primary counters

        [JsonProperty("docType")]
        public string Type { get; set; } // "PrimaryCounter";
        
        public PrimaryCounter(string counterId ,string counterName, long startvalue)
        {
            this.Id = counterId;
            this.PK = this.Id;
            this.CounterName = counterName;
            this.Counter_StartValue=startvalue;
            this.Type = "PrimaryCounter";
            this.Status = CounterStatus.pending;
        }

    }

    public class DistributedCounter
    {
        [JsonProperty("id")]
        public string Id { get; set; } //will be GUID

        [JsonProperty("pk")]
        public string ParentCounter { get; set; } //will be PK

        [JsonProperty("_etag")]
        public string ETag { get; set; } //Can I update or has someone else taken the lock

        [JsonProperty("countervalue")]
        public long Value { get; set; }

        [JsonProperty("status")]
        public CounterStatus Status { get; set; } // status of  the distributed counter

        [JsonProperty("docType")]
        public string Type { get; set; } //DistributedCounter;


        public DistributedCounter( long value, string parentCounterId)
        {
            this.Id = Guid.NewGuid().ToString();
            this.ParentCounter = parentCounterId;
            this.Value = value;
            this.Type = "DistributedCounter";
            this.Status = CounterStatus.active;
        }
    }   

}
