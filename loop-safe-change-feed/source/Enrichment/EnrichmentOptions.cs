namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// Configuration for the change-feed enrichment pattern. Every property name is configurable so
/// the pattern is generic: point <see cref="SourceProperty"/> at whatever field drives the
/// derived value, and store the result in <see cref="EnrichedProperty"/> alongside a loop-guard
/// hash in <see cref="HashProperty"/>.
/// </summary>
public sealed class EnrichmentOptions
{
    /// <summary>Database that holds both the documents and the change-feed lease container.</summary>
    public string DatabaseName { get; set; } = "EnrichDB";

    /// <summary>Container that is both the source of changes and the target of in-place writes.</summary>
    public string ContainerName { get; set; } = "Documents";

    /// <summary>Lease container used by the change feed processor to track its position.</summary>
    public string LeaseContainerName { get; set; } = "leases";

    /// <summary>The document property whose value drives enrichment (the "source" input).</summary>
    public string SourceProperty { get; set; } = "text";

    /// <summary>The property where the derived value (here, an identicon SVG) is written.</summary>
    public string EnrichedProperty { get; set; } = "identicon";

    /// <summary>
    /// The property that stores the hash of the source. This is the loop guard: after an
    /// enrichment write re-triggers the change feed, the recomputed source hash matches the
    /// stored one, so the change is skipped instead of causing an endless loop.
    /// </summary>
    public string HashProperty { get; set; } = "sourceHash";

    /// <summary>Logical name of the change feed processor (stored in the lease documents).</summary>
    public string ProcessorName { get; set; } = "enrichment";
}
