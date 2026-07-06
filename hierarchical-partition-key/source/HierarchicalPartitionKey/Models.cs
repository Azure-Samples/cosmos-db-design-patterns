using Newtonsoft.Json;

namespace Cosmos.HierarchicalPartitionKey;

/// <summary>
/// A user activity event in a multi-tenant SaaS app. The hierarchical partition key is
/// <c>/tenantId</c> then <c>/userId</c>: the tenant is the top level (so tenant-scoped access is
/// targeted), and the user sub-level lets a tenant grow beyond a single logical partition.
/// </summary>
public sealed class ActivityEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The kind of read/query, used to annotate what Cosmos DB did (and why it matters).</summary>
public enum QueryKind
{
    PointRead,
    TenantPrefix,
    TenantAndUser,
    CrossPartition,
    SingleKeyCrossPartition,
}

/// <summary>
/// The outcome of a read/query: the items, the RU charge, the text/shape of the operation, and a
/// teaching note describing how Cosmos DB targeted (or fanned out across) partitions.
/// </summary>
public sealed class QueryResult
{
    public QueryKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string QueryText { get; init; } = string.Empty;
    public string PartitionKeyUsed { get; init; } = string.Empty;
    public bool Targeted { get; init; }
    public double RequestCharge { get; init; }
    public int Count { get; init; }
    public string Note { get; init; } = string.Empty;
    public List<ActivityEvent> Items { get; init; } = new();
}

/// <summary>A node in the tenant → user distribution tree (how data maps to the hierarchy).</summary>
public sealed class TenantNode
{
    public string TenantId { get; init; } = string.Empty;
    public int TotalEvents { get; set; }
    public List<UserNode> Users { get; init; } = new();
}

public sealed class UserNode
{
    public string UserId { get; init; } = string.Empty;
    public int Events { get; init; }
}
