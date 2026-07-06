// Adapted for .NET 10 from CloudDistributedLock by Brian Dunnington, used under the MIT License.
// https://github.com/briandunnington/CloudDistributedLock

namespace Cosmos.DistributedLock
{
    /// <summary>
    /// A single document that represents a held lock. Its <c>id</c> is the (sanitized) lock
    /// name, so an insert succeeds only when no one else currently holds the lock. The
    /// <c>_ttl</c> field lets Azure Cosmos DB automatically delete the record (releasing the
    /// lock) if the holder stops renewing it.
    /// </summary>
    internal class LockRecord
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? providerName { get; set; }
        public DateTimeOffset lockObtainedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset lockLastRenewedAt { get; set; } = DateTimeOffset.UtcNow;
        public int _ttl { get; set; }
    }
}
