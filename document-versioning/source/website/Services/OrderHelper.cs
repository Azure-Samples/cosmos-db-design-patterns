using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Versioning
{
    public class OrderHelper
    {
        private CosmosClient client;
        private Database? database;
        private Container? container;
        private string databaseName = "Orders";
        private string containerName = "CurrentOrderStatus";
        private string partitionKey = "/CustomerId";

        public OrderHelper(){
            client = new CosmosClient(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);           
        }

        public Order GenerateOrder() {
            var order = new Order();
            Random rng = new Random();
            order.OrderId = Guid.NewGuid().ToString();            
            order.CustomerId = rng.Next(10,20);
            order.Status = "Submitted";
            order.OrderDetails = new List<OrderItem>();
            int orderItemCount = rng.Next(1, 5);
            for (int i=0; i < orderItemCount; i++)
            {
                order.OrderDetails.Add(GenerateOrderItem());
            }
            return order;
        }

        private VersionedOrder HandleVersioning(Order order){
            VersionedOrder versionedOrder;
            if (order is not VersionedOrder){
                versionedOrder = new VersionedOrder(order);
            } else {
                versionedOrder = (VersionedOrder)order;
                versionedOrder.DocumentVersion++;
            }
            return versionedOrder;
        }

        public VersionedOrder CancelOrder(VersionedOrder order) {
            order.Status = "Cancelled";
            return HandleVersioning(order);
        }

        public VersionedOrder FulfillOrder(VersionedOrder order) {
            order.Status = "Fulfilled";
            return HandleVersioning(order);
        }

        public VersionedOrder DeliverOrder(VersionedOrder order) {
            order.Status = "Delivered";
            return HandleVersioning(order);
        }

        public OrderItem GenerateOrderItem()
        {
            var orderItem = new OrderItem();
            Random rng = new Random();
            var productId = rng.Next(1, 25);
            orderItem.ProductName = $"Product {productId}";
            orderItem.Quantity = rng.Next(1, 5);
            orderItem.Price = Math.Round(rng.NextDouble() * rng.Next(5,20) * orderItem.Quantity,2);
            return orderItem;
        }

        public async Task<IEnumerable<VersionedOrder>> RetrieveAllOrdersAsync(){
            database = await client.CreateDatabaseIfNotExistsAsync(id: databaseName);
            container = await database.CreateContainerIfNotExistsAsync(
                id: containerName,
                partitionKeyPath: partitionKey,
                throughput: 400
            );   
            List<VersionedOrder> orders = new();
            using FeedIterator<VersionedOrder> feed = container.GetItemQueryIterator<VersionedOrder>(
                queryText: "SELECT * FROM Orders"
            );
            while (feed.HasMoreResults)
            {
                FeedResponse<VersionedOrder> response = await feed.ReadNextAsync();

                // Iterate query results
                foreach (VersionedOrder order in response)
                {
                    orders.Add(order);
                }
            }
            return orders;
        }

        public async Task<VersionedOrder> RetrieveOrderAsync(string orderId, int customerId){
            database = await client.CreateDatabaseIfNotExistsAsync(id: databaseName);
            container = await database.CreateContainerIfNotExistsAsync(
                id: containerName,
                partitionKeyPath: partitionKey,
                throughput: 400
            );
            IOrderedQueryable<VersionedOrder> ordersQueryable = container.GetItemLinqQueryable<VersionedOrder>();
            var matches = ordersQueryable
                .Where(order => order.CustomerId == customerId)
                .Where(order => order.OrderId == orderId);
            using FeedIterator<VersionedOrder> orderFeed = matches.ToFeedIterator();
            VersionedOrder selectedOrder = new VersionedOrder();
            while (orderFeed.HasMoreResults)
            {
                FeedResponse<VersionedOrder> response = await orderFeed.ReadNextAsync();
                if (response.Count > 0)
                {
                    selectedOrder = response.Resource.First();
                }
            }
            
            //return orderResponse.Resource;
            return selectedOrder;
        }

        public async Task<Order> SaveOrder(Order orderToUpdate)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(id: databaseName);
            container = await database.CreateContainerIfNotExistsAsync(
                id: containerName,
                partitionKeyPath: partitionKey,
                throughput: 400
            );
            return await container.UpsertItemAsync(orderToUpdate);
        }

        public async Task<VersionedOrder> SaveVersionedOrder(VersionedOrder orderToUpdate)
        {
            database = await client.CreateDatabaseIfNotExistsAsync(id: databaseName);
            container = await database.CreateContainerIfNotExistsAsync(
                id: containerName,
                partitionKeyPath: partitionKey,
                throughput: 400
            );
            return await container.UpsertItemAsync(orderToUpdate);
        }
    }
}