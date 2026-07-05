using Newtonsoft.Json;

namespace EventSourcing
{
    public class CartEvent
    {
        [JsonProperty("id")]
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CartId { get; set; } = Guid.NewGuid().ToString();  //Partition Key
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public int UserId { get; set; }
        public string EventType { get; set; } = "";
        public string? Product { get; set; }
        public int? QuantityChange { get; set; } = 0;
        public List<CartItem>? ProductsInCart { get; set; }
        public string EventTimestamp { get; set; } = DateTime.UtcNow.ToString();
    }
}
