namespace Versioning
{
    public class CartItem {
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }

        public CartItem() { }

        public CartItem(string productName, int quantity)
        {
            ProductName = productName;
            Quantity = quantity;
        }
    }
}