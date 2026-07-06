using Cosmos.TransactionalOutbox;
using Microsoft.Extensions.Configuration;

// -----------------------------------------------------------------------------------------------
// Azure Cosmos DB — Transactional Outbox sample.
//
// Placing an order must both change state (the Order) and publish an event (OrderPlaced) that other
// systems consume. Doing those as two separate writes is a "dual-write": if the app crashes in
// between, the event is lost. The transactional outbox writes the order AND the event in one atomic
// TransactionalBatch, then a change feed processor relays the event downstream — so it can never be
// lost, even if the app crashes right after the commit.
//
// This program places orders three ways to show the difference.
// -----------------------------------------------------------------------------------------------

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string endpoint = config["CosmosUri"] ?? string.Empty;
string? key = config["CosmosKey"];

if (string.IsNullOrWhiteSpace(endpoint))
{
    endpoint = "https://localhost:8081";
    if (string.IsNullOrEmpty(key))
    {
        key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    }
}

Console.WriteLine("Azure Cosmos DB — Transactional Outbox (order placement)");
Console.WriteLine();

await using var service = new OrderOutboxService(endpoint, key);
await service.EnsureStartedAsync();

async Task PlaceBatch(string title, OrderMode mode, bool crash, int count)
{
    Console.WriteLine($"--- {title} ---");
    for (int i = 0; i < count; i++)
    {
        await service.PlaceOrderAsync(mode, crash);
    }
    await Task.Delay(TimeSpan.FromSeconds(3)); // let the change feed relay any outbox events
    Console.WriteLine();
}

await PlaceBatch("Naive dual-write, no crash — events reach downstream", OrderMode.NaiveDualWrite, crash: false, count: 3);
await PlaceBatch("Naive dual-write, WITH a crash after saving the order — events LOST", OrderMode.NaiveDualWrite, crash: true, count: 3);
await PlaceBatch("Transactional outbox, WITH a crash after the commit — events still delivered", OrderMode.TransactionalOutbox, crash: true, count: 3);

// Give the relay a final moment to drain the outbox.
await Task.Delay(TimeSpan.FromSeconds(3));

Console.WriteLine("================ RESULT ================");
Console.WriteLine($"Orders placed:     {service.OrdersPlaced}");
Console.WriteLine($"Events delivered:  {service.EventsDelivered}   (downstream consumer)");
Console.WriteLine($"Events LOST:       {service.EventsLost}   (naive dual-write + crash)");
Console.WriteLine();
Console.WriteLine("The 3 naive+crash orders lost their events; the 3 outbox+crash orders did not —");
Console.WriteLine("their events were committed with the order and relayed by the change feed.");
