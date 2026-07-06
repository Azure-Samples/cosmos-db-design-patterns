using Cosmos.VectorSearch;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

// -----------------------------------------------------------------------------------------------
// Azure Cosmos DB — Vector Search (semantic search) sample.
//
// Movies are stored with a 384-dimensional vector embedding of their title + plot, computed by a
// small LOCAL model (no API key, no cloud call). Azure Cosmos DB indexes the vectors, so we can
// find movies by MEANING with VectorDistance() — a query like "space battle with aliens" surfaces
// the sci-fi films even though it shares no words with their titles. We also show a filtered
// search (genre + vector ranking in a single query).
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

var options = new VectorSearchOptions();

Console.WriteLine("Azure Cosmos DB — Vector Search (semantic movie search)");
Console.WriteLine();

using CosmosClient client = CosmosClientFactory.Create(endpoint, key);
Console.WriteLine($"Ensuring {options.DatabaseName}/{options.ContainerName} exists (with a vector index)...");
await MovieVectorStore.EnsureContainerAsync(client, options);

Console.WriteLine("Loading the local embedding model (bge-micro-v2, 384-dim)...");
using var embeddings = new EmbeddingService();
var store = new MovieVectorStore(client, embeddings, options);

// Seed the catalog on first run. Each movie's embedding is computed locally, then stored.
int existing = await store.CountAsync();
if (existing < MovieCatalog.All.Count)
{
    Console.WriteLine($"Embedding and storing {MovieCatalog.All.Count} movies...");
    foreach (Movie movie in MovieCatalog.All)
    {
        await store.UpsertAsync(movie);
    }
}
else
{
    Console.WriteLine($"{existing} movies already stored.");
}
Console.WriteLine();

async Task RunAsync(string query, string? genre = null)
{
    string header = genre is null ? $"🔎  \"{query}\"" : $"🔎  \"{query}\"  (genre = {genre})";
    Console.WriteLine(header);
    List<SearchResult> results = await store.SearchAsync(query, genre, top: 4);
    foreach (SearchResult r in results)
    {
        Console.WriteLine($"    {r.Score:0.000}  {r.Title} ({r.Year}) · {r.Genre}");
    }
    Console.WriteLine();
}

// A few semantic queries — note none of these words appear in the movie titles.
await RunAsync("space battle with aliens and starships");
await RunAsync("a detective hunting a serial killer");
await RunAsync("two people falling in love in a city");
await RunAsync("a bumbling robbery that goes wrong");
await RunAsync("scary haunted house at night");

// The same query, restricted to one genre — a metadata filter + vector ranking in one query.
await RunAsync("a magical journey to save the world", genre: "Fantasy");

Console.WriteLine("Done. Every result was ranked by meaning, not keywords.");
