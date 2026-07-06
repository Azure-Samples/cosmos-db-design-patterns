using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Net;
using Xunit;

namespace CosmosDesignPatterns.Tests.DistributedLock;

// ---------------------------------------------------------------------------
// Model – mirrors the single lock record used by the distributed-lock pattern
// (adapted from CloudDistributedLock by Brian Dunnington, MIT License).
// ---------------------------------------------------------------------------

/// <summary>
/// A single document that represents a held lock. Its <c>id</c> is the lock name, so an
/// insert only succeeds when no one else currently holds the lock. The <c>_ttl</c> field
/// lets Cosmos DB auto-delete it (releasing the lock) if the holder stops renewing.
/// </summary>
public class LockRecord
{
    [JsonProperty("id")]
    public string? id { get; set; }
    [JsonProperty("name")]
    public string? name { get; set; }
    [JsonProperty("lockObtainedAt")]
    public DateTimeOffset lockObtainedAt { get; set; } = DateTimeOffset.UtcNow;
    [JsonProperty("lockLastRenewedAt")]
    public DateTimeOffset lockLastRenewedAt { get; set; } = DateTimeOffset.UtcNow;
    [JsonProperty("ttl")]
    public int _ttl { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the (reworked) Distributed Lock design pattern.
///
/// The pattern stores a single record per lock in a TTL-enabled container:
///   - Acquiring the lock is a <c>CreateItem</c>; a 409 Conflict means it is already held.
///   - The holder renews the lock with <c>ReplaceItem</c> + ETag (optimistic concurrency).
///   - Releasing the lock is a <c>DeleteItem</c> + ETag.
///   - The Cosmos session token (global LSN) provides a monotonically increasing fencing token.
///
/// These tests verify:
///   - A lock can be acquired when it is not held.
///   - A second acquire attempt fails with 409 Conflict while the lock is held.
///   - Renewing with the current ETag succeeds; renewing with a stale ETag fails.
///   - Releasing (deleting) the record lets the lock be acquired again.
///   - The session token / fencing token increases monotonically across writes.
/// </summary>
public class DistributedLockTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"DistributedLockTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public DistributedLockTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        ContainerProperties props = new()
        {
            Id = "Locks",
            PartitionKeyPath = "/id",
            DefaultTimeToLive = -1  // TTL enabled; each lock record controls its own expiry via _ttl.
        };
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync(props));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers – mirror CosmosLockClient in the pattern source.
    // -----------------------------------------------------------------------

    private async Task<ItemResponse<LockRecord>?> TryAcquireAsync(string lockName, int ttl = 30)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var record = new LockRecord { id = lockName, name = lockName, lockObtainedAt = now, lockLastRenewedAt = now, _ttl = ttl };
            return await _container.CreateItemAsync(record, new PartitionKey(lockName));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }
    }

    private async Task<ItemResponse<LockRecord>?> RenewAsync(ItemResponse<LockRecord> item)
    {
        try
        {
            var existing = item.Resource;
            var record = new LockRecord
            {
                id = existing.id,
                name = existing.name,
                lockObtainedAt = existing.lockObtainedAt,
                lockLastRenewedAt = DateTimeOffset.UtcNow,
                _ttl = existing._ttl
            };
            return await _container.ReplaceItemAsync(record, record.id, new PartitionKey(record.id), new ItemRequestOptions { IfMatchEtag = item.ETag });
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task ReleaseAsync(ItemResponse<LockRecord> item)
    {
        await _container.DeleteItemAsync<LockRecord>(item.Resource.id, new PartitionKey(item.Resource.id), new ItemRequestOptions { IfMatchEtag = item.ETag });
    }

    // Mirrors SessionTokenParser: extract the global LSN from the Cosmos session token.
    private static long ParseSessionToken(string sessionToken)
    {
        var items = sessionToken.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var segments = items[1].Split('#', StringSplitOptions.RemoveEmptyEntries);
        var globalLsnIndex = segments.Length == 1 ? 0 : 1;
        return long.Parse(segments[globalLsnIndex]);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Acquire_Succeeds_WhenLockNotHeld()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";

        var item = await TryAcquireAsync(lockName);

        Assert.NotNull(item);
        Assert.Equal(lockName, item!.Resource.id);
    }

    [Fact]
    public async Task Acquire_Fails_WhenLockAlreadyHeld()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";

        var first = await TryAcquireAsync(lockName);
        Assert.NotNull(first);

        // Second attempt should fail (409 Conflict) because the lock is held.
        var second = await TryAcquireAsync(lockName);
        Assert.Null(second);
    }

    [Fact]
    public async Task Renew_Succeeds_WithMatchingEtag_And_Fails_WithStaleEtag()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";

        var item = await TryAcquireAsync(lockName);
        Assert.NotNull(item);

        // Renew with the current ETag succeeds and advances lockLastRenewedAt.
        var renewed = await RenewAsync(item!);
        Assert.NotNull(renewed);
        Assert.True(renewed!.Resource.lockLastRenewedAt >= item!.Resource.lockLastRenewedAt);

        // Renewing again with the ORIGINAL (now stale) ETag fails.
        var staleRenew = await RenewAsync(item);
        Assert.Null(staleRenew);
    }

    [Fact]
    public async Task Release_Deletes_Record_And_Allows_Reacquire()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";

        var item = await TryAcquireAsync(lockName);
        Assert.NotNull(item);

        // While held, another acquire fails.
        Assert.Null(await TryAcquireAsync(lockName));

        await ReleaseAsync(item!);

        // After release, the lock can be acquired again.
        var reacquired = await TryAcquireAsync(lockName);
        Assert.NotNull(reacquired);
    }

    [Fact]
    public async Task FencingToken_MonotonicallyIncreases_AcrossWrites()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";

        var item = await TryAcquireAsync(lockName);
        Assert.NotNull(item);
        long token1 = ParseSessionToken(item!.Headers.Session);

        var renewed = await RenewAsync(item);
        Assert.NotNull(renewed);
        long token2 = ParseSessionToken(renewed!.Headers.Session);

        Assert.True(token2 >= token1, "The fencing token (session LSN) must not decrease across writes.");
    }
}
