
namespace Versioning
{
    public class CartHelper
    {
        public static Cart GenerateCart() {
            var cart = new Cart();
            Random rng = new Random();
            cart.Id = Guid.NewGuid().ToString();
            cart.SessionId= Guid.NewGuid().ToString();
            cart.CustomerId = rng.Next(1,999);
            cart.Items = new List<CartItem>();
            int cartItemCount = rng.Next(1, 5);
            for (int i=0; i < cartItemCount; i++)
            {
                cart.Items.Add(GenerateCartItem());
            }
            return cart;
        }
        public static CartItem GenerateCartItem()
        {
            var cartItem = new CartItem();
            Random rng = new Random();
            var productId = rng.Next(1, 25);
            cartItem.ProductName = $"Product {productId}";
            cartItem.Quantity = rng.Next(1, 5);
            return cartItem;
        }

        public static CartWithVersion GenerateVersionedCart()
        {
            var cart = new CartWithVersion();
            Random rng = new Random();
            cart.Id = Guid.NewGuid().ToString();
            cart.SessionId = Guid.NewGuid().ToString();
            cart.CustomerId = rng.Next(1, 999);
            cart.Items = new List<CartItemWithSpecialOrder>();
            int cartItemCount = rng.Next(1, 5);
            for (int i = 0; i < cartItemCount; i++)
            {
                cart.Items.Add(GenerateCartItemWithSpecialOrder());
            }
            return cart;
        }

        public static CartItemWithSpecialOrder GenerateCartItemWithSpecialOrder()
        {
            var cartItem = new CartItemWithSpecialOrder();
            Random rng = new Random();
            var productId = rng.Next(1, 25);
            cartItem.ProductName = $"Product {productId}";
            cartItem.Quantity = rng.Next(1, 5);
            bool[] boolValues = new bool[] {true, false};
            cartItem.IsSpecialOrder = boolValues[rng.Next(0,2)];
            if (cartItem.IsSpecialOrder) { 
                cartItem.SpecialOrderNotes = $"Special Order Details for {cartItem.ProductName}";  
            }
            return cartItem;
        }
    }
}