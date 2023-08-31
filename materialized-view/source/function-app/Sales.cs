using System;

namespace MaterializedViews {

    public class Sales {    
        public string id { get; set; }  = Guid.NewGuid().ToString();
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public DateTime OrderDate {get; set;} = DateTime.UtcNow;
        public int Qty { get; set;}
        public string Product { get; set; }
        public double Total { get; set; }
    }

    public class SalesByProduct {
        public string id { get; set; }  = Guid.NewGuid().ToString();
        public DateTime OrderDate { get; set; }
        public int Qty { get; set;}
        public double Total { get; set; }
        public string Product { get; set; }
        
        public SalesByProduct(){}
        public SalesByProduct(Sales salesItem){
                this.OrderDate = salesItem.OrderDate;
                this.Product = salesItem.Product;
                this.Qty = salesItem.Qty;
                this.Total = salesItem.Total;                
        }
    }   
}