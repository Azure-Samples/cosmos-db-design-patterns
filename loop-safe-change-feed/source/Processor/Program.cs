using Cosmos.ChangeFeedEnrichment;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Net;

// -----------------------------------------------------------------------------------------------
// Loop-Safe Change Feed Enrichment — console sample.
//
// A Change Feed Processor watches the "Documents" container and writes an enriched value (here a
// deterministic identicon SVG derived from a hash of the source text) back onto the SAME document
// in the SAME container. Writing to the same container you read from would normally loop forever,
// because each write is itself a change. The loop is broken by hashing the SOURCE text: after the
// enrichment write re-triggers the feed, the recomputed source hash matches the stored hash, so
// the change is skipped instead of re-enriched.
//
// This program starts the processor and then makes a few source edits so you can watch, in the
// log, one ENRICHED per real edit followed by exactly one SKIPPED echo. The writes-vs-skips
// summary at the end shows the loop is bounded.
// -----------------------------------------------------------------------------------------------

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string endpoint = config["CosmosUri"] ?? string.Empty;
string? key = config["CosmosKey"];

// Default to the local Cosmos DB emulator when nothing is configured (zero-setup local runs).
if (string.IsNullOrWhiteSpace(endpoint))
{
    endpoint = "https://localhost:8081";
    if (string.IsNullOrEmpty(key))
    {
        key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    }
}

var options = new EnrichmentOptions();

Console.WriteLine("Azure Cosmos DB — Loop-Safe Change Feed Enrichment");
Console.WriteLine();
Console.WriteLine($"Ensuring {options.DatabaseName}/{options.ContainerName} (and lease container) exist...");

using CosmosClient client = CosmosClientFactory.Create(endpoint, key);
await EnrichmentBootstrapper.EnsureContainersAsync(client, options);
Container container = client.GetContainer(options.DatabaseName, options.ContainerName);

var consoleLock = new object();
void Log(ProcessedChange c)
{
    (ConsoleColor color, string label) = c.Kind switch
    {
        EnrichmentKind.Enriched => (ConsoleColor.Green, "ENRICHED "),
        EnrichmentKind.Skipped => (ConsoleColor.DarkGray, "SKIPPED  "),
        _ => (ConsoleColor.Yellow, "SUPERSED."),
    };

    string detail = c.Kind switch
    {
        EnrichmentKind.Enriched => $"source hash {Short(c.OldHash)} -> {Short(c.NewHash)}  (\"{c.SourceValue}\")",
        EnrichmentKind.Skipped => $"source hash unchanged ({Short(c.NewHash)}) — echo of our own write, no re-enrichment",
        _ => "a newer edit superseded this change; the newer one will enrich",
    };

    lock (consoleLock)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{c.Timestamp.LocalDateTime:HH:mm:ss}] {label} {c.DocumentId,-8} writes={c.TotalWrites} skips={c.TotalSkips}  {detail}");
        Console.ResetColor();
    }
}

static string Short(string? hash) => string.IsNullOrEmpty(hash) ? "(none)" : hash[..Math.Min(8, hash.Length)];

await using var processor = new ChangeFeedEnrichmentProcessor(client, options);
processor.Processed += Log;

Console.WriteLine("Starting the change feed processor (Ctrl+C to stop)...");
Console.WriteLine();
await processor.StartAsync();

// Give the processor a moment to acquire leases before we start making edits.
await Task.Delay(TimeSpan.FromSeconds(3));

async Task WriteSourceAsync(string id, string text)
{
    // Read-modify-write so an edit preserves the other fields (including the stored hash), exactly
    // as a real application editing only the source text would. That lets the log show the real
    // hash transition (old -> new) rather than starting from scratch each time.
    JObject doc;
    try
    {
        ItemResponse<JObject> existing = await container.ReadItemAsync<JObject>(id, new PartitionKey(id));
        doc = existing.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        doc = new JObject { ["id"] = id };
    }

    doc[options.SourceProperty] = text;
    await container.UpsertItemAsync(doc, new PartitionKey(id));
    lock (consoleLock)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] EDIT     {id,-8} set {options.SourceProperty} = \"{text}\"");
        Console.ResetColor();
    }
}

// A few source edits. Watch each one produce exactly one ENRICHED then one SKIPPED echo.
var edits = new (string Id, string Text)[]
{
    ("doc-1", "Hello, Cosmos!"),
    ("doc-1", "Hello, Change Feed!"),
    ("doc-2", "Loop-safe enrichment"),
    ("doc-1", "Hashing the source breaks the loop"),
};

foreach ((string id, string text) in edits)
{
    await WriteSourceAsync(id, text);
    await Task.Delay(TimeSpan.FromSeconds(5));
}

// Let any final echo settle, then summarize.
await Task.Delay(TimeSpan.FromSeconds(3));

Console.WriteLine();
Console.WriteLine($"Summary: {processor.Writes} enrichment writes, {processor.Skips} skipped echoes.");
Console.WriteLine($"We made {edits.Length} source edits and wrote back {processor.Writes} times — one enrichment per");
Console.WriteLine("edit, each followed by a skipped echo. The change feed did not loop.");
Console.WriteLine();
Console.WriteLine("Done.");
