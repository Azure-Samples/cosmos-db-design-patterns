namespace Cosmos.HierarchicalPartitionKey;

/// <summary>Configuration for the hierarchical-partition-key sample.</summary>
public sealed class HpkOptions
{
    public string DatabaseName { get; set; } = "ActivityDB";

    /// <summary>Container partitioned hierarchically by <c>/tenantId</c> then <c>/userId</c>.</summary>
    public string HierarchicalContainer { get; set; } = "Events";

    /// <summary>The "before" container, partitioned by a single key <c>/userId</c>, for comparison.</summary>
    public string SingleKeyContainer { get; set; } = "EventsByUser";
}
