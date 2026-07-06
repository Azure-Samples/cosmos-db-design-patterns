using Newtonsoft.Json;

namespace Cosmos.PatchApi;

/// <summary>
/// An order document whose fields are owned by <em>different</em> services: payment sets
/// <c>paymentStatus</c>, shipping sets <c>shippingStatus</c>/<c>trackingNumber</c>, analytics
/// increments <c>viewCount</c>, and merchandising appends <c>tags</c>. Because each service only
/// ever touches its own field, the Patch API lets them update the document independently — without
/// reading it first and without clobbering each other's writes.
/// </summary>
public sealed class Order
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("customer")] public string Customer { get; set; } = string.Empty;
    [JsonProperty("product")] public string Product { get; set; } = string.Empty;
    [JsonProperty("amount")] public decimal Amount { get; set; }

    // Owned by the payment service.
    [JsonProperty("paymentStatus")] public string PaymentStatus { get; set; } = "Pending";

    // Owned by the shipping service.
    [JsonProperty("shippingStatus")] public string ShippingStatus { get; set; } = "NotShipped";
    [JsonProperty("trackingNumber")] public string? TrackingNumber { get; set; }

    // Owned by analytics — a monotonic counter.
    [JsonProperty("viewCount")] public int ViewCount { get; set; }

    // Owned by merchandising — an append-only list.
    [JsonProperty("tags")] public List<string> Tags { get; set; } = new();

    // Records which write last touched the document (used to make the lost-update visible).
    [JsonProperty("lastWriteBy")] public string LastWriteBy { get; set; } = string.Empty;
}

/// <summary>How two concurrent, different-field updates are applied.</summary>
public enum RaceMode
{
    /// <summary>Each service reads the whole document, changes its field, and replaces the whole
    /// document. Without optimistic concurrency the second writer overwrites the first — a lost update.</summary>
    ReadModifyWrite,

    /// <summary>Each service patches only its own field. No read, nothing to clobber — both survive.</summary>
    Patch,
}

/// <summary>The RU cost of the same logical update done two ways (measured from the emulator/account).</summary>
public sealed record RuComparison(double PatchRu, double ReadRu, double ReplaceRu)
{
    public double ReadModifyWriteRu => ReadRu + ReplaceRu;
}

/// <summary>The outcome of the concurrency race: the final field values and whether an update was lost.</summary>
public sealed record RaceResult(
    RaceMode Mode,
    string PaymentStatus,
    string ShippingStatus,
    bool PaymentLost,
    bool ShippingLost)
{
    public bool AnyLost => PaymentLost || ShippingLost;
}

public enum LogLevel { Info, Success, Warn, Error }

/// <summary>One line in the activity timeline.</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, string Message, LogLevel Level);
