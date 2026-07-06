using Cosmos.DistributedLock;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// -----------------------------------------------------------------------------------------
// Cosmos DB Distributed Lock sample.
//
// This demo starts several workers that all compete for the same lock. Only one can hold it
// at a time. The holder keeps the lock alive automatically (a background keep-alive renews it)
// even when its work runs longer than the lock's TTL, so no other worker can start until it
// releases. Each acquisition prints a monotonically increasing fencing token.
// -----------------------------------------------------------------------------------------

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string endpoint = config["CosmosUri"] ?? string.Empty;
string? key = config["CosmosKey"];

const string databaseName = "LockDB";
const string containerName = "Locks";
const string lockName = "my-resource";
const int ttlSeconds = 5;

Console.WriteLine("Azure Cosmos DB distributed lock sample");
Console.WriteLine();

// Ensure the database and (TTL-enabled) container exist. In Azure these are pre-created by
// azd; locally against the emulator this creates them on first run.
Console.WriteLine("Ensuring the LockDB/Locks container exists...");
using (CosmosClient bootstrap = CosmosClientFactory.Create(endpoint, key))
{
    Database db = await bootstrap.CreateDatabaseIfNotExistsAsync(databaseName);
    await db.CreateContainerIfNotExistsAsync(new ContainerProperties
    {
        Id = containerName,
        PartitionKeyPath = "/id",
        DefaultTimeToLive = -1  // TTL enabled; each lock record sets its own _ttl.
    });
}

// Register the lock provider with dependency injection.
var services = new ServiceCollection();
services.AddCosmosDistributedLock(endpoint, key, databaseName, ttlSeconds, o => o.ContainerName = containerName);
using ServiceProvider serviceProvider = services.BuildServiceProvider();
var lockProviderFactory = serviceProvider.GetRequiredService<ICosmosDistributedLockProviderFactory>();

var consoleLock = new object();
void Log(ConsoleColor color, string message)
{
    lock (consoleLock)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        Console.ResetColor();
    }
}

// --- Phase 1: three workers competing for the same lock for 25 seconds. ---
Console.WriteLine($"Starting three workers competing for lock '{lockName}' (TTL {ttlSeconds}s) for 25 seconds...");
Console.WriteLine();

var workers = new[]
{
    ("Worker-A", ConsoleColor.Cyan),
    ("Worker-B", ConsoleColor.Magenta),
    ("Worker-C", ConsoleColor.Green),
};

using var runFor = new CancellationTokenSource(TimeSpan.FromSeconds(25));

async Task RunWorkerAsync(string name, ConsoleColor color)
{
    ICosmosDistributedLockProvider provider = lockProviderFactory.GetLockProvider();
    var rng = new Random(name.GetHashCode());

    while (!runFor.IsCancellationRequested)
    {
        // Try to acquire the lock immediately.
        using (CosmosDistributedLock @lock = await provider.TryAcquireLockAsync(lockName))
        {
            if (@lock.IsAcquired)
            {
                // Intentionally hold the lock LONGER than the TTL to show the keep-alive
                // renewing it. No other worker can acquire while we hold it.
                int holdMs = rng.Next(ttlSeconds * 1000, ttlSeconds * 2000);
                Log(color, $"{name}: ACQUIRED lock (fencing token {@lock.FencingToken}); holding for {holdMs} ms (TTL {ttlSeconds}s, auto-renewed)");
                await Task.Delay(holdMs);
                Log(color, $"{name}: releasing lock");
            }
            else
            {
                Log(color, $"{name}: could not acquire (held by another worker)");
            }
        }

        try
        {
            await Task.Delay(500, runFor.Token);
        }
        catch (TaskCanceledException)
        {
            break;
        }
    }
}

await Task.WhenAll(workers.Select(w => RunWorkerAsync(w.Item1, w.Item2)));

// --- Phase 2: demonstrate waiting with a timeout. ---
Console.WriteLine();
Console.WriteLine("Demonstrating AcquireLockAsync with a 2-second wait timeout...");
ICosmosDistributedLockProvider waitProvider = lockProviderFactory.GetLockProvider();
using (CosmosDistributedLock held = await waitProvider.TryAcquireLockAsync(lockName))
{
    Log(ConsoleColor.White, held.IsAcquired
        ? $"Holder: acquired lock (fencing token {held.FencingToken}) and will hold it for 5 seconds"
        : "Holder: unexpectedly could not acquire the lock");

    // A second caller waits up to 2 seconds, then gives up because the lock is still held.
    using CosmosDistributedLock waiter = await waitProvider.AcquireLockAsync(lockName, TimeSpan.FromSeconds(2));
    Log(ConsoleColor.White, waiter.IsAcquired
        ? "Waiter: acquired the lock (unexpected — it was held)"
        : "Waiter: gave up after 2 seconds because the lock was held (as expected)");
}

Console.WriteLine();
Console.WriteLine("Done.");
