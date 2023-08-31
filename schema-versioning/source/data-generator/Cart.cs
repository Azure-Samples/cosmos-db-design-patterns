using Newtonsoft.Json;

namespace Versioning
{
    public class Cart
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public int CustomerId { get; set; }
        public List<CartItem>? Items { get; set;}
    }
}