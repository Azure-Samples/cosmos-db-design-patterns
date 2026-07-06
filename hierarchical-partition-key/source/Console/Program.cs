using Cosmos.HierarchicalPartitionKey;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

// -----------------------------------------------------------------------------------------------
// Azure Cosmos DB — Hierarchical Partition Key sample.
//
// Multi-tenant activity events are stored with a HIERARCHICAL partition key: /tenantId then
// /userId. Leading with tenantId means tenant-scoped queries (the everyday workload of a SaaS app)
// stay targeted, while the /userId sub-level lets a tenant grow past the 20 GB limit of a single
// logical partition and spreads load instead of creating a hot partition.
//
// This program seeds data and runs the reads/queries a real app performs, reporting for each what
// Cosmos DB did — a targeted read of specific partitions, or a fan-out across all of them.
//
// NOTE: the emulator runs each container on ONE physical partition, so the RU numbers below look
// similar regardless of targeting. The targeting difference is the lesson; at production scale it
// becomes a real cost/latency difference (and only the hierarchical key scales past 20 GB/tenant).
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

var options = new HpkOptions();

Console.WriteLine("Azure Cosmos DB — Hierarchical Partition Key (multi-tenant activity events)");
Console.WriteLine();

using CosmosClient client = CosmosClientFactory.Create(endpoint, key);
Console.WriteLine($"Ensuring {options.DatabaseName} containers exist ('{options.HierarchicalContainer}' = /tenantId/userId, '{options.SingleKeyContainer}' = /userId)...");
await ActivityStore.EnsureContainersAsync(client, options);

var store = new ActivityStore(client, options);

const int tenants = 8, usersPerTenant = 6, eventsPerUser = 25;
if (await store.CountAsync() < tenants * usersPerTenant * eventsPerUser)
{
    Console.WriteLine($"Seeding {tenants} tenants x {usersPerTenant} users x {eventsPerUser} events into both containers...");
    await store.SeedAsync(tenants, usersPerTenant, eventsPerUser);
}
Console.WriteLine();

void Print(QueryResult r)
{
    Console.ForegroundColor = r.Targeted ? ConsoleColor.Green : ConsoleColor.Yellow;
    Console.WriteLine($"{(r.Targeted ? "TARGETED " : "FAN-OUT  ")} {r.Title}");
    Console.ResetColor();
    Console.WriteLine($"    query:         {r.QueryText}");
    Console.WriteLine($"    partition key: {r.PartitionKeyUsed}");
    Console.WriteLine($"    result:        {r.Count} events, {r.RequestCharge:0.00} RU");
    Console.WriteLine($"    why:           {r.Note}");
    Console.WriteLine();
}

string tenant = "tenant-03", user = "user-03-02";

Print(await store.PointReadAsync(tenant, user));
Print(await store.TenantPrefixAsync(tenant));
Print(await store.TenantAndUserAsync(tenant, user));
Print(await store.CrossPartitionByActionAsync("purchase"));

Console.WriteLine("--- Before/after: the same tenant dashboard query, two partition designs ---");
Console.WriteLine();
Print(await store.SingleKeyTenantAsync(tenant));   // before: /userId container, fans out
Print(await store.TenantPrefixAsync(tenant));       // after: hierarchical, targeted

Console.WriteLine("On the emulator the RU is similar (single physical partition). At production");
Console.WriteLine("scale the fan-out queries cost more with every partition, and only the");
Console.WriteLine("hierarchical key lets a tenant grow beyond a single 20 GB logical partition.");
