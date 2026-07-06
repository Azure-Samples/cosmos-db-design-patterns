namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// A single observation from the processor, raised for every document the change feed delivers.
/// Consumers (the console logger, the web timeline) use it to visualize why the loop is bounded:
/// one <see cref="EnrichmentKind.Enriched"/> per real source change, followed by exactly one
/// <see cref="EnrichmentKind.Skipped"/> (the echo of that write).
/// </summary>
/// <param name="DocumentId">The document's id.</param>
/// <param name="Kind">What happened (enriched, skipped, or superseded).</param>
/// <param name="OldHash">The hash stored on the document before this change.</param>
/// <param name="NewHash">The recomputed source hash.</param>
/// <param name="SourceValue">The source value (included for enriched changes so UIs can show it).</param>
/// <param name="TotalWrites">Running count of write-backs since the processor started.</param>
/// <param name="TotalSkips">Running count of skipped echoes since the processor started.</param>
/// <param name="Timestamp">When the change was processed (UTC).</param>
public sealed record ProcessedChange(
    string DocumentId,
    EnrichmentKind Kind,
    string? OldHash,
    string? NewHash,
    string? SourceValue,
    long TotalWrites,
    long TotalSkips,
    DateTimeOffset Timestamp);
