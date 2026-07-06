using Microsoft.Azure.Cosmos;

namespace Cosmos.VectorSearch;

/// <summary>
/// Application service behind the Blazor UI. It owns the Cosmos client, the local embedding model,
/// and the movie store; ensures the (vector-indexed) container exists and is seeded once; and
/// exposes a simple search method for the page.
/// </summary>
public sealed class VectorSearchAppService : IAsyncDisposable
{
    private readonly string _endpoint;
    private readonly string? _key;
    private readonly VectorSearchOptions _options = new();

    private CosmosClient? _client;
    private EmbeddingService? _embeddings;
    private MovieVectorStore? _store;
    private int _started;

    public VectorSearchAppService(IConfiguration config)
    {
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

        _endpoint = endpoint;
        _key = key;
    }

    public bool IsReady => Volatile.Read(ref _started) == 2;

    /// <summary>All genres in the catalog, for the filter dropdown.</summary>
    public IReadOnlyList<string> Genres { get; } =
        MovieCatalog.All.Select(m => m.Genre).Distinct().OrderBy(g => g).ToList();

    /// <summary>
    /// Creates the vector-indexed container, loads the embedding model, and seeds the catalog once.
    /// Loading the model up front means the first search is fast.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _client = CosmosClientFactory.Create(_endpoint, _key);
            await MovieVectorStore.EnsureContainerAsync(_client, _options, cts.Token);

            _embeddings = new EmbeddingService();
            _store = new MovieVectorStore(_client, _embeddings, _options);

            if (await _store.CountAsync() < MovieCatalog.All.Count)
            {
                foreach (Movie movie in MovieCatalog.All)
                {
                    await _store.UpsertAsync(movie, cts.Token);
                }
            }

            Volatile.Write(ref _started, 2);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Could not reach Azure Cosmos DB at {_endpoint}. If you're running locally, start the emulator with 'docker compose up -d' from the repo root, then reload. ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    public Task<List<SearchResult>> SearchAsync(string query, string? genre, int top = 8)
    {
        if (_store is null || string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new List<SearchResult>());
        }

        return _store.SearchAsync(query, genre, top);
    }

    public ValueTask DisposeAsync()
    {
        _embeddings?.Dispose();
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
