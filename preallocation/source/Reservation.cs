using Newtonsoft.Json;

namespace Cosmos_Patterns_Preallocation
{
    public class Reservation
    {
        public Reservation()
        {
            EntityType = "reservation";
        }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("_etag")]
        public string? ETag { get; set; }

        public string? EntityType { get; set; }
        public string? LeaseId { get; set; }
        public DateTime? LeasedUntil { get; set; }

        public bool IsPaid { get; set; }

        public string? Status { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }


        [JsonProperty("hotelId")]
        public string? HotelId { get; set; }

        public string? RoomId { get; set; }
        public Room? Room { get; set; }
    }
}