using Newtonsoft.Json;

namespace Versioning {
    public class CartWithVersion
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public long CustomerId { get; set; }
        public List<CartItemWithSpecialOrder>? Items { get; set;}
        public int SchemaVersion = 2;
    }
}