namespace Versioning
{
    public class OrderItem {
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; } = default!;

        public double Price { get; set; } = default!;

        public OrderItem() { }

        public OrderItem(string productName, int quantity, double price)
        {
            ProductName = productName;
            Quantity = quantity;
            Price = price;
        }
    }
}