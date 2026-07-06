using Newtonsoft.Json.Linq;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>The kind of action the pattern took (or would take) for a document.</summary>
public enum EnrichmentKind
{
    /// <summary>The source changed: the derived value and hash were (re)computed and written.</summary>
    Enriched,

    /// <summary>The source hash matched the stored hash, so nothing was written (loop broken).</summary>
    Skipped,

    /// <summary>A concurrent update superseded this change; the newer version re-triggers the feed.</summary>
    Superseded,
}

/// <summary>The outcome of evaluating a single document against the enrichment rule.</summary>
public sealed class EnrichmentDecision
{
    private EnrichmentDecision(bool shouldWrite, EnrichmentKind kind, string? oldHash, string? newHash)
    {
        ShouldWrite = shouldWrite;
        Kind = kind;
        OldHash = oldHash;
        NewHash = newHash;
    }

    /// <summary>True when the source changed and the document must be written back.</summary>
    public bool ShouldWrite { get; }

    public EnrichmentKind Kind { get; }

    /// <summary>The hash previously stored on the document (null on first sight).</summary>
    public string? OldHash { get; }

    /// <summary>The freshly computed source hash.</summary>
    public string? NewHash { get; }

    internal static EnrichmentDecision Enriched(string? oldHash, string newHash) =>
        new(true, EnrichmentKind.Enriched, oldHash, newHash);

    internal static EnrichmentDecision Skipped(string? hash) =>
        new(false, EnrichmentKind.Skipped, hash, hash);
}

/// <summary>
/// The heart of the pattern, isolated from Cosmos DB so it is trivial to reason about and test.
/// It compares the current source hash to the one stored on the document:
/// <list type="bullet">
///   <item>hashes differ (or none stored) → compute the derived value + new hash in place, write.</item>
///   <item>hashes match → skip. This is the echo of our own write, so writing again would loop.</item>
/// </list>
/// The hash is taken over the source property ONLY — never the derived value or the hash itself —
/// so that writing the derived value does not change the hash and therefore cannot re-trigger work.
/// </summary>
public sealed class DocumentEnricher
{
    private readonly EnrichmentOptions _options;

    public DocumentEnricher(EnrichmentOptions options) => _options = options;

    /// <summary>
    /// Evaluates <paramref name="document"/> and, when the source changed, mutates it in place by
    /// setting the enriched property and the stored hash. Returns what was decided.
    /// </summary>
    public EnrichmentDecision Evaluate(JObject document)
    {
        string? sourceValue = document[_options.SourceProperty]?.ToString();
        string newHash = SourceHasher.Compute(sourceValue);
        string? storedHash = document[_options.HashProperty]?.ToString();

        if (string.Equals(newHash, storedHash, StringComparison.Ordinal))
        {
            return EnrichmentDecision.Skipped(storedHash);
        }

        document[_options.EnrichedProperty] = IdenticonGenerator.ToSvg(newHash);
        document[_options.HashProperty] = newHash;
        return EnrichmentDecision.Enriched(storedHash, newHash);
    }
}
