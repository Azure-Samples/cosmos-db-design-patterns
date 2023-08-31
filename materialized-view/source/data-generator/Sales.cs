namespace MaterializedViews {

    public class Sales {    
        public string id { get; set; }  = Guid.NewGuid().ToString();
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public DateTime OrderDate {get; set;} = DateTime.UtcNow;
        public int Qty { get; set;}
        public string Product { get; set; } = default!;
        public double Total { get; set; }        
    }

    public class SalesHelper{

        internal static Dictionary<string,double> Products = new Dictionary<string, double>{
            {"Widget", 6.00},
            {"Gizmo", 5.00},
            {"Thing", 3.00}
        };
        public static Sales GenerateSales() {
            var sales = new Sales();
            Random random = new Random();
            sales.CustomerId = random.Next(10,20);
            sales.OrderId = random.Next(1000,9000);
            sales.Product = Products.Keys.ElementAt(random.Next(Products.Keys.Count));
            sales.Qty = random.Next(1,15);
            sales.Total = Products[sales.Product] * sales.Qty;
            return sales;
        }
    }
}