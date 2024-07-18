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
    }
}