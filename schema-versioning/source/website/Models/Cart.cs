using Newtonsoft.Json;

namespace Versioning{
    public class Cart
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public long CustomerId { get; set; }
        public List<CartItemWithSpecialOrder>? Items { get; set;}
        public int? SchemaVersion {get; set;}
        public bool HasSpecialOrders() { 
            return this.Items.Where(x=>x.IsSpecialOrder == true).Count() > 0;
        }
    }
}