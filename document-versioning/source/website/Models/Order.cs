namespace Versioning {
    public class Order {    
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; } = default!;
        public DateTime OrderDate {get; set;} = DateTime.UtcNow;
        public int CustomerId { get; set;}
        public string Status { get; set; } = default!;
        public List<OrderItem>? OrderDetails { get; set;  }
    }
}