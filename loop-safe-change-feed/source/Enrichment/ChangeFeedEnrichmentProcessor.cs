using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// Hosts an Azure Cosmos DB <b>Change Feed Processor</b> (no Azure Functions required) that reads
/// changes from a container and writes the enriched result back to the <b>same</b> container. The
/// naive version of that would loop forever, because each write is itself a change. The loop is
/// broken by <see cref="DocumentEnricher"/>: the echo of our own write recomputes the same source
/// hash, matches the stored hash, and is skipped.
/// </summary>
public sealed class ChangeFeedEnrichmentProcessor : IAsyncDisposable
{
    private readonly Container _container;
    private readonly Container _leaseContainer;
    private readonly DocumentEnricher _enricher;
    private readonly EnrichmentOptions _options;

    private ChangeFeedProcessor? _processor;
    private long _writes;
    private long _skips;

    /// <summary>Raised for every document delivered by the change feed.</summary>
    public event Action<ProcessedChange>? Processed;

    public long Writes => Interlocked.Read(ref _writes);
    public long Skips => Interlocked.Read(ref _skips);

    public ChangeFeedEnrichmentProcessor(CosmosClient client, EnrichmentOptions options)
    {
        _options = options;
        _container = client.GetContainer(options.DatabaseName, options.ContainerName);
        _leaseContainer = client.GetContainer(options.DatabaseName, options.LeaseContainerName);
        _enricher = new DocumentEnricher(options);
    }

    /// <summary>Starts processing. Reads existing documents too, so pre-created data is enriched.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _processor = _container
            .GetChangeFeedProcessorBuilder<JObject>(_options.ProcessorName, HandleChangesAsync)
            .WithInstanceName($"host-{Guid.NewGuid():N}")
            .WithLeaseContainer(_leaseContainer)
            .WithStartTime(DateTime.MinValue.ToUniversalTime())
            .WithPollInterval(TimeSpan.FromSeconds(1))
            .Build();

        await _processor.StartAsync();
    }

    private async Task HandleChangesAsync(
        IReadOnlyCollection<JObject> changes,
        CancellationToken cancellationToken)
    {
        foreach (JObject document in changes)
        {
            string id = document["id"]?.ToString() ?? string.Empty;
            if (id.Length == 0)
            {
                continue;
            }

            EnrichmentDecision decision = _enricher.Evaluate(document);

            if (!decision.ShouldWrite)
            {
                long skips = Interlocked.Increment(ref _skips);
                Raise(new ProcessedChange(id, EnrichmentKind.Skipped, decision.OldHash, decision.NewHash,
                    null, Writes, skips, DateTimeOffset.UtcNow));
                continue;
            }

            try
            {
                // Optimistic concurrency: if the user edited the source again between the feed read
                // and this write, the ETag no longer matches, so we skip and let the newer change
                // (already queued in the feed) re-trigger enrichment with the correct source.
                await _container.ReplaceItemAsync(
                    document,
                    id,
                    new PartitionKey(id),
                    new ItemRequestOptions { IfMatchEtag = document["_etag"]?.ToString() },
                    cancellationToken);

                long writes = Interlocked.Increment(ref _writes);
                Raise(new ProcessedChange(id, EnrichmentKind.Enriched, decision.OldHash, decision.NewHash,
                    document[_options.SourceProperty]?.ToString(), writes, Skips, DateTimeOffset.UtcNow));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                Raise(new ProcessedChange(id, EnrichmentKind.Superseded, decision.OldHash, decision.NewHash,
                    null, Writes, Skips, DateTimeOffset.UtcNow));
            }
        }
    }

    private void Raise(ProcessedChange change)
    {
        try
        {
            Processed?.Invoke(change);
        }
        catch
        {
            // A misbehaving observer must never break change feed processing.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.StopAsync();
        }
    }
}
