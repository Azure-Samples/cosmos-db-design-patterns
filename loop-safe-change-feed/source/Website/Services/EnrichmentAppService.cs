using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// A lightweight document projection for the UI: the source text plus the derived identicon and
/// the loop-guard hash (once the processor has enriched it).
/// </summary>
public sealed record DocView(string Id, string Text, string? IdenticonSvg, string? Hash);

/// <summary>
/// Application service behind the Blazor UI. It owns the Cosmos client and the change feed
/// processor (started once), keeps a rolling log of processed changes for the activity timeline,
/// and provides simple create/edit/delete operations so users can drive the pattern from a browser.
/// </summary>
public sealed class EnrichmentAppService : IAsyncDisposable
{
    private readonly string _endpoint;
    private readonly string? _key;
    private readonly EnrichmentOptions _options = new();
    private readonly LinkedList<ProcessedChange> _log = new();
    private readonly object _sync = new();

    private CosmosClient? _client;
    private Container? _container;
    private ChangeFeedEnrichmentProcessor? _processor;
    private int _started;

    /// <summary>Raised whenever the processor reports a change, so the UI can refresh.</summary>
    public event Action? Changed;

    public EnrichmentAppService(IConfiguration config)
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

    public EnrichmentOptions Options => _options;
    public long Writes => _processor?.Writes ?? 0;
    public long Skips => _processor?.Skips ?? 0;

    /// <summary>Creates the containers and starts the change feed processor exactly once.</summary>
    public async Task EnsureStartedAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            _client = CosmosClientFactory.Create(_endpoint, _key);
            await EnrichmentBootstrapper.EnsureContainersAsync(_client, _options, cts.Token);
            _container = _client.GetContainer(_options.DatabaseName, _options.ContainerName);

            _processor = new ChangeFeedEnrichmentProcessor(_client, _options);
            _processor.Processed += OnProcessed;
            await _processor.StartAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Could not reach Azure Cosmos DB at {_endpoint}. If you're running locally, start the emulator with 'docker compose up -d' from the repo root, then reload. ({ex.GetType().Name}: {ex.Message})", ex);
        }
    }

    private void OnProcessed(ProcessedChange change)
    {
        lock (_sync)
        {
            _log.AddFirst(change);
            while (_log.Count > 100)
            {
                _log.RemoveLast();
            }
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<ProcessedChange> Timeline()
    {
        lock (_sync)
        {
            return _log.ToList();
        }
    }

    /// <summary>Returns all documents (newest first) projected for the UI.</summary>
    public async Task<List<DocView>> GetDocumentsAsync()
    {
        var results = new List<DocView>();
        if (_container is null)
        {
            return results;
        }

        using FeedIterator<JObject> feed = _container.GetItemQueryIterator<JObject>(
            "SELECT * FROM c ORDER BY c._ts DESC");
        while (feed.HasMoreResults)
        {
            foreach (JObject doc in await feed.ReadNextAsync())
            {
                results.Add(new DocView(
                    doc["id"]?.ToString() ?? string.Empty,
                    doc[_options.SourceProperty]?.ToString() ?? string.Empty,
                    doc[_options.EnrichedProperty]?.ToString(),
                    doc[_options.HashProperty]?.ToString()));
            }
        }

        return results;
    }

    /// <summary>Creates a new document with the given source text.</summary>
    public async Task<string> AddDocumentAsync(string text)
    {
        if (_container is null)
        {
            throw new InvalidOperationException("The service is not started.");
        }

        string id = $"doc-{Guid.NewGuid():N}"[..12];
        var doc = new JObject { ["id"] = id, [_options.SourceProperty] = text };
        await _container.UpsertItemAsync(doc, new PartitionKey(id));
        return id;
    }

    /// <summary>
    /// Updates only the source text, preserving the other fields (read-modify-write) — just as a
    /// real application editing the source would. The processor then re-enriches on the next change.
    /// </summary>
    public async Task SaveSourceAsync(string id, string text)
    {
        if (_container is null)
        {
            throw new InvalidOperationException("The service is not started.");
        }

        JObject doc;
        try
        {
            ItemResponse<JObject> existing = await _container.ReadItemAsync<JObject>(id, new PartitionKey(id));
            doc = existing.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            doc = new JObject { ["id"] = id };
        }

        doc[_options.SourceProperty] = text;
        await _container.UpsertItemAsync(doc, new PartitionKey(id));
    }

    public async Task DeleteDocumentAsync(string id)
    {
        if (_container is null)
        {
            return;
        }

        try
        {
            await _container.DeleteItemAsync<JObject>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
        }

        _client?.Dispose();
    }
}
