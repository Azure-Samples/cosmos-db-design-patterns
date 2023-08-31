using Newtonsoft.Json;

namespace Cosmos_Patterns_Preallocation
{
    public class Room
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("_etag")]
        public string? ETag { get; set; }

        public string? EntityType { get; set; }
        public string? LeaseId { get; set; }
        public DateTime? LeasedUntil { get; set; }
        [JsonProperty("hotelId")]
        public string? HotelId { get; set; }
        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Status { get; set; }

        public int NoBeds { get; set; }

        public int SizeInSqFt { get; set; }
        public decimal Price { get; set; }
        public bool Available { get; set; }
        public string? Description { get; set; }
        public int MaximumGuests { get; set; }

        public List<AvailableDate> AvailableDates { get; set; }
        
        public Room()
        {
            EntityType = "room";
            AvailableDates = new List<AvailableDate>();
        }

    }

    public class AvailableDate
    {
        public DateTime Date { get; set; }

        public bool IsAvailable { get; set; }
    }

    public class RoomAttibuteBased : Room
    {
        public decimal Price_USD { get; set; }

        public decimal Price_EUR { get; set; }

        public decimal Price_BTC { get; set; }

        public decimal Size_Meters { get; set; }

        public decimal Size_SquareFeet { get; set; }
    }

    public class RoomNonAttibuteBased : Room
    {
        public List<RoomPrice> RoomPrices = new List<RoomPrice>();
        public List<RoomSize> RoomSizes = new List<RoomSize>();
    }

    public class RoomPrice
    {
        public string? Currency { get; set; }

        public decimal Price { get; set; }
    }

    public class RoomSize
    {
        public string? Measurement { get; set; }

        public decimal Size { get; set; }
    }
}