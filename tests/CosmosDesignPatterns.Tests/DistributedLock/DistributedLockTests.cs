using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Net;
using Xunit;

namespace CosmosDesignPatterns.Tests.DistributedLock;

// ---------------------------------------------------------------------------
// Models – mirror the distributed-lock pattern source models
// ---------------------------------------------------------------------------

/// <summary>
/// The lock document.  Its <c>id</c> is the lock name and it is its own
/// partition key so optimistic-concurrency patching works correctly.
/// </summary>
public class DistributedLockDoc
{
    [JsonProperty("id")]
    public string LockName { get; set; } = string.Empty;
    [JsonProperty("OwnerId")]
    public string OwnerId { get; set; } = string.Empty;
    [JsonProperty("FenceToken")]
    public long FenceToken { get; set; }
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}

/// <summary>
/// The lease document.  Its <c>id</c> is the owner id and the <c>ttl</c>
/// field causes Cosmos DB to auto-expire it when the lease duration elapses.
/// </summary>
public class LockLease
{
    [JsonProperty("id")]
    public string OwnerId { get; set; } = string.Empty;
    [JsonProperty("ttl")]
    public int LeaseDuration { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Distributed Lock design pattern.
///
/// The pattern uses two document types in a single TTL-enabled container:
///   • <see cref="DistributedLockDoc"/> – the lock itself, identified by a
///     logical lock name.  A monotonically increasing FenceToken lets
///     callers detect stale lock holders.
///   • <see cref="LockLease"/> – a short-lived document (TTL) that proves a
///     particular owner is still alive.  Cosmos DB automatically deletes it
///     when the lease period expires.
///
/// These tests verify:
///   - A new lock document can be created with fence token 1.
///   - Acquiring the lock increments the fence token.
///   - The fence token increments monotonically across multiple acquisitions.
///   - A valid owner + fence token pair passes validation.
///   - An outdated fence token fails validation.
///   - Deleting a lease (simulating expiry) causes validation to fail.
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
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Partition key is /id (same as the source pattern's CosmosService).
        // TTL is enabled so lease documents auto-expire in real usage.
        ContainerProperties props = new()
        {
            Id = "Locks",
            PartitionKeyPath = "/id",
            DefaultTimeToLive = -1  // enable TTL; individual docs control their own expiry
        };
        _container = await db.CreateContainerIfNotExistsAsync(props);
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<DistributedLockDoc> CreateLockAsync(string lockName, string ownerId)
    {
        var doc = new DistributedLockDoc
        {
            LockName = lockName,
            OwnerId = ownerId,
            FenceToken = 1
        };
        var response = await _container.CreateItemAsync(doc, new PartitionKey(lockName));
        return response.Resource;
    }

    private async Task<DistributedLockDoc?> ReadLockAsync(string lockName)
    {
        try
        {
            var r = await _container.ReadItemAsync<DistributedLockDoc>(lockName, new PartitionKey(lockName));
            return r.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Transfers ownership to <paramref name="newOwnerId"/> and increments the
    /// fence token using Patch + optimistic concurrency (mirrors CosmosService.UpdateLockAsync).
    /// </summary>
    private async Task<DistributedLockDoc> AcquireLockAsync(DistributedLockDoc current, string newOwnerId)
    {
        var operations = new List<PatchOperation>
        {
            PatchOperation.Set("/OwnerId", newOwnerId),
            PatchOperation.Increment("/FenceToken", 1L)
        };

        return await _container.PatchItemAsync<DistributedLockDoc>(
            current.LockName,
            new PartitionKey(current.LockName),
            operations,
            new PatchItemRequestOptions { IfMatchEtag = current.ETag });
    }

    private async Task CreateLeaseAsync(string ownerId, int ttlSeconds = 30)
    {
        var lease = new LockLease { OwnerId = ownerId, LeaseDuration = ttlSeconds };
        await _container.UpsertItemAsync(lease, new PartitionKey(ownerId));
    }

    private async Task<bool> LeaseExistsAsync(string ownerId)
    {
        try
        {
            await _container.ReadItemAsync<LockLease>(ownerId, new PartitionKey(ownerId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateLock_Stores_DocumentWithFenceTokenOne()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string ownerId = Guid.NewGuid().ToString();

        var doc = await CreateLockAsync(lockName, ownerId);

        Assert.Equal(lockName, doc.LockName);
        Assert.Equal(ownerId, doc.OwnerId);
        Assert.Equal(1, doc.FenceToken);
    }

    [Fact]
    public async Task AcquireLock_Increments_FenceToken()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string owner1 = Guid.NewGuid().ToString();
        string owner2 = Guid.NewGuid().ToString();

        var original = await CreateLockAsync(lockName, owner1);
        Assert.Equal(1, original.FenceToken);

        var updated = await AcquireLockAsync(original, owner2);

        Assert.Equal(owner2, updated.OwnerId);
        Assert.Equal(2, updated.FenceToken);
    }

    [Fact]
    public async Task FenceToken_MonotonicallyIncrements_AcrossMultipleAcquisitions()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string owner = Guid.NewGuid().ToString();

        var current = await CreateLockAsync(lockName, owner);

        for (int i = 2; i <= 5; i++)
        {
            current = await AcquireLockAsync(current, Guid.NewGuid().ToString());
            Assert.Equal(i, current.FenceToken);
        }
    }

    [Fact]
    public async Task ValidOwner_And_ValidFenceToken_PassesValidation()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string ownerId = Guid.NewGuid().ToString();

        await CreateLeaseAsync(ownerId);
        var lockDoc = await CreateLockAsync(lockName, ownerId);

        // Validation mirrors DistributedLockService.ValidateLeaseAsync:
        // the fence token must match and a lease must exist for the owner.
        var current = await ReadLockAsync(lockName);
        Assert.NotNull(current);
        Assert.Equal(ownerId, current.OwnerId);
        Assert.Equal(lockDoc.FenceToken, current.FenceToken);

        bool leaseExists = await LeaseExistsAsync(ownerId);
        Assert.True(leaseExists);
    }

    [Fact]
    public async Task OutdatedFenceToken_FailsValidation()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string owner1 = Guid.NewGuid().ToString();
        string owner2 = Guid.NewGuid().ToString();

        var lockDoc = await CreateLockAsync(lockName, owner1);
        long staleToken = lockDoc.FenceToken;   // = 1

        // Owner2 acquires the lock → fence token becomes 2.
        await AcquireLockAsync(lockDoc, owner2);

        var current = await ReadLockAsync(lockName);
        Assert.NotNull(current);

        // A caller holding the old fence token (1) should detect that a newer
        // token (2) exists, meaning its lock is no longer valid.
        Assert.True(current.FenceToken > staleToken,
            "A newer fence token must be present after the lock was re-acquired.");
    }

    [Fact]
    public async Task DeletingLease_Simulates_Expiry_And_Causes_ValidationFailure()
    {
        string lockName = $"lock-{Guid.NewGuid():N}";
        string ownerId = Guid.NewGuid().ToString();

        await CreateLeaseAsync(ownerId);
        await CreateLockAsync(lockName, ownerId);

        // Simulate TTL expiry by deleting the lease document.
        await _container.DeleteItemAsync<LockLease>(ownerId, new PartitionKey(ownerId));

        bool leaseStillExists = await LeaseExistsAsync(ownerId);
        Assert.False(leaseStillExists, "Lease should not exist after deletion.");

        // Without a valid lease the lock can be taken over by another owner.
        var current = await ReadLockAsync(lockName);
        Assert.NotNull(current);
        var newOwnerLock = await AcquireLockAsync(current, Guid.NewGuid().ToString());
        Assert.NotEqual(ownerId, newOwnerLock.OwnerId);
    }
}
