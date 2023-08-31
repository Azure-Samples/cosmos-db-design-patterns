using System.Collections.Generic;
using System;

namespace Versioning {
    public class Order {    
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; }
        public DateTime OrderDate {get; set;} = DateTime.UtcNow;
        public int CustomerId { get; set;}
        public string Status { get; set; }
        public List<OrderItem> OrderDetails { get; set;  }        
    }
}