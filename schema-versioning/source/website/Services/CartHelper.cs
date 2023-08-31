using Microsoft.Azure.Cosmos;

namespace Versioning;

public class CartHelper{

    public CartHelper(){}

    public async static Task<IEnumerable<Cart>> RetrieveAllCartsAsync(){
        CosmosHelper cosmosHelper = new();
        Container container = cosmosHelper.GetContainer().Result;
        List<Cart> carts = new();
        using FeedIterator<Cart> feed = container.GetItemQueryIterator<Cart>(
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