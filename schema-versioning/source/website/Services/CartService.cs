using Microsoft.Azure.Cosmos;
using Versioning.Models;

namespace Versioning.Services
{ 
    public class CartService{

        private readonly CosmosDbService _cosmosDbService;

        public CartService(CosmosDbService cosmosDbService)
        {
            _cosmosDbService = cosmosDbService;
        }

        public async Task<IEnumerable<Cart>> RetrieveAllCartsAsync()
        {
            
            List<Cart> carts = new();
        
            using FeedIterator<Cart> feed = _cosmosDbService.CartsContainer.GetItemQueryIterator<Cart>(
                queryText: "SELECT * FROM Carts"
            );

            while (feed.HasMoreResults)
            {
                FeedResponse<Cart> response = await feed.ReadNextAsync();

                // Iterate query results
                foreach (Cart cart in response)
                {
                    carts.Add(cart);
                }
            }
            return carts;
        }

        private static readonly string[] Products =
        {
            "Wireless Mouse", "Mechanical Keyboard", "USB-C Cable", "Laptop Stand", "Monitor Arm",
            "Standing Desk", "Office Chair", "Notebook", "Fountain Pen", "Desk Lamp", "Webcam",
            "Headset", "Docking Station", "Whiteboard", "Coffee Mug"
        };

        private static readonly string[] SpecialNotes =
        {
            "Engrave with customer name", "Custom color: navy", "Gift wrap and include a note",
            "Firm foam, extra lumbar support", "Left-handed configuration", "Rush delivery requested"
        };

        /// <summary>
        /// Creates a cart. A versioned (v2) cart carries <c>SchemaVersion = 2</c> and may include
        /// special-order items with notes; an original (v1) cart has no schema version and plain
        /// items. Both shapes live in the same container &mdash; that's the pattern.
        /// </summary>
        public async Task<Cart> AddCartAsync(bool versioned)
        {
            Random rng = Random.Shared;
            Cart cart = new()
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = Guid.NewGuid().ToString(),
                CustomerId = rng.Next(1000, 9999),
                SchemaVersion = versioned ? 2 : null,
                Items = new List<CartItemWithSpecialOrder>()
            };

            int itemCount = rng.Next(2, 5);
            for (int i = 0; i < itemCount; i++)
            {
                CartItemWithSpecialOrder item = new()
                {
                    ProductName = Products[rng.Next(Products.Length)],
                    Quantity = rng.Next(1, 4)
                };

                // Only versioned carts can carry special orders (the v2 addition).
                if (versioned && rng.Next(0, 2) == 0)
                {
                    item.IsSpecialOrder = true;
                    item.SpecialOrderNotes = SpecialNotes[rng.Next(SpecialNotes.Length)];
                }

                cart.Items.Add(item);
            }

            await _cosmosDbService.CartsContainer.UpsertItemAsync(cart, new PartitionKey(cart.Id));
            return cart;
        }

        /// <summary>Deletes all carts so the demo can be reset from the browser.</summary>
        public async Task ClearAllAsync()
        {
            foreach (Cart cart in await RetrieveAllCartsAsync())
            {
                await _cosmosDbService.CartsContainer.DeleteItemAsync<Cart>(cart.Id, new PartitionKey(cart.Id));
            }
        }
    }
}