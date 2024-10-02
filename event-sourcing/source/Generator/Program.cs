using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;


namespace EventSourcing
{

    internal class Program
    {
        static string urlBase = "http://localhost:7071";  //7086

        public static async Task<string> CreateCartEvent(HttpClient client, CartEvent cartEvent)
        {
            var url = $"{urlBase}/api/EventSourceFunction";

            string jsonBody = JsonConvert.SerializeObject(cartEvent);
            var body = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            Console.WriteLine(jsonBody);

            try
            {
                var response = await client.PostAsync(url, body);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    string errorResult = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorResult}");
                    return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
                }

                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return $"Exception: {ex.Message}";
            }
        }

        public static List<CartEvent> GenerateCartEvents()
        {
            var cartEvents = new List<CartEvent>();
            Random rng = new Random();
            string[] actions = new string[]
            {
                "cart_created",
                "product_added",
                "product_deleted",
                "cart_purchased"
            };
            var cartId = Guid.NewGuid().ToString();
            var sessionId = Guid.NewGuid().ToString();
            var userId = rng.Next(1, 999);

            int product1Qty = rng.Next(5);
            int product2Qty = rng.Next(5);

            foreach (var action in actions)
            {
                if (action.StartsWith("product"))
                {
                    if (action.Contains("added"))
                    {
                        // Add to products                        
                        var cartEvent1 = new CartEvent();
                        cartEvent1.CartId = cartId;
                        cartEvent1.SessionId = sessionId;
                        cartEvent1.EventType = action;
                        cartEvent1.UserId = userId;
                        cartEvent1.Product = "Product 1";
                        cartEvent1.QuantityChange = product1Qty;
                        cartEvent1.ProductsInCart = new List<CartItem>
                        {
                            new CartItem("Product 1", product1Qty)
                        };
                        cartEvents.Add(cartEvent1);
                        var cartEvent2 = new CartEvent();
                        cartEvent2.CartId = cartId;
                        cartEvent2.SessionId = sessionId;
                        cartEvent2.UserId = userId;
                        cartEvent2.EventType = action;
                        cartEvent2.Product = "Product 2";
                        cartEvent2.QuantityChange = product2Qty;
                        cartEvent2.ProductsInCart = new List<CartItem>
                        {
                            new CartItem("Product 1", product1Qty),
                            new CartItem("Product 2", product2Qty)
                        };
                        cartEvents.Add(cartEvent2);
                    }
                    else
                    {
                        // Delete last product
                        var cartEvent = new CartEvent();
                        cartEvent.CartId = cartId;
                        cartEvent.SessionId = sessionId;
                        cartEvent.UserId = userId;
                        cartEvent.EventType = action;
                        cartEvent.Product = "Product 2";
                        cartEvent.QuantityChange = -1;
                        cartEvent.ProductsInCart = new List<CartItem>
                        {
                            new CartItem("Product 1", product1Qty)
                        };
                        cartEvents.Add(cartEvent);
                    }
                }
                else
                {
                    var cartEvent = new CartEvent();
                    cartEvent.CartId = cartId;
                    cartEvent.SessionId = sessionId;
                    cartEvent.UserId = userId;
                    cartEvent.EventType = action;
                    cartEvent.ProductsInCart =  new List<CartItem>();
                    cartEvents.Add(cartEvent);
                }
            }
            return cartEvents;
        }

        static async Task Main(string[] args)
        {

            if (args.Length > 0 && args[0] != "console")
            {
                var host = new HostBuilder()
                //.ConfigureFunctionsWebApplication()
                .ConfigureServices(services =>
                {
                    //services.AddApplicationInsightsTelemetryWorkerService();
                    //services.ConfigureFunctionsApplicationInsights();
                })
                .Build();

                await host.RunAsync();
            }

            else
            {
                HttpClient httpClient = new HttpClient();


            httpClient.Timeout = TimeSpan.FromMinutes(10);

            Console.WriteLine("This code will demonstrate the Event Sourcing pattern by saving shopping cart events to Azure Cosmos DB for NoSQL account.");

            Console.WriteLine("How many sets of cart events should be created?");
            var cartCount = Console.ReadLine();

            int.TryParse(cartCount, out int numOfCartEventSets);

            for (int i = 0; i < numOfCartEventSets; i++)
            {
                // Create a list of cart events
                List<CartEvent> cartEvents = GenerateCartEvents();

                // Send each event to function which writes to Azure Cosmos DB for NoSQL
                foreach (var cartEvent in cartEvents)
                {
                    var result1 = await CreateCartEvent(httpClient, cartEvent);
                    System.Console.WriteLine(result1);
                }
            }

            System.Console.WriteLine($"Function completed generation of shopping cart events");
            Console.WriteLine($"Check CartEventContainer for new shopping cart events");
            }
        }
    }
}
