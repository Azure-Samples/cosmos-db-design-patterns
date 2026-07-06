using Newtonsoft.Json;

namespace Cosmos.TransactionalOutbox;

/// <summary>How an order is placed: the naive dual-write, or the transactional outbox.</summary>
public enum OrderMode
{
    /// <summary>Save the order, then publish the event as a separate step (risk of a lost event).</summary>
    NaiveDualWrite,

    /// <summary>Save the order AND the event atomically, then let the change feed publish it.</summary>
    TransactionalOutbox,
}

/// <summary>The business state — an order. Its <c>id</c> equals its <c>orderId</c> (the partition key).</summary>
public sealed class Order
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("docType")] public string DocType { get; set; } = "order";
    [JsonProperty("customer")] public string Customer { get; set; } = string.Empty;
    [JsonProperty("product")] public string Product { get; set; } = string.Empty;
    [JsonProperty("amount")] public decimal Amount { get; set; }
    [JsonProperty("status")] public string Status { get; set; } = "Placed";
    [JsonProperty("placedAt")] public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// An outbox event, written into the SAME container (and same partition, <c>orderId</c>) as the
/// order — atomically, in the transactional outbox mode. The change feed relays it downstream.
/// </summary>
public sealed class OutboxEvent
{
    [JsonProperty("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonProperty("orderId")] public string OrderId { get; set; } = string.Empty;
    [JsonProperty("docType")] public string DocType { get; set; } = "event";
    [JsonProperty("eventType")] public string EventType { get; set; } = "OrderPlaced";
    [JsonProperty("customer")] public string Customer { get; set; } = string.Empty;
    [JsonProperty("product")] public string Product { get; set; } = string.Empty;
    [JsonProperty("amount")] public decimal Amount { get; set; }
    [JsonProperty("createdAt")] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>An event as received by the (simulated, external) downstream consumer.</summary>
public sealed record DeliveredEvent(string Id, string OrderId, string EventType, string Customer, string Product, DateTimeOffset DeliveredAt);

public enum LogLevel { Info, Success, Warn, Error }

/// <summary>One line in the activity timeline.</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, string Message, LogLevel Level);
