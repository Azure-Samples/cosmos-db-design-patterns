using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CosmosDesignPatterns.Tests.LoopSafeChangeFeed;

/// <summary>
/// Integration tests for the Loop-Safe Change Feed design pattern.
///
/// The pattern enriches a document and writes the result back to the SAME container it was read
/// from. Doing that naively loops forever, because each write is itself a change. The loop is
/// broken by storing a hash of the SOURCE property on the document: when the change feed delivers
/// the echo of our own write, the recomputed source hash matches the stored hash, so the change is
/// skipped instead of re-enriched.
///
/// Like the other pattern tests, these simulate what the Change Feed processor does with direct
/// container operations (mirroring <c>DocumentEnricher</c> in the pattern source), which keeps the
/// tests deterministic and free of change-feed timing flakiness.
///
/// These tests verify:
///   - A new document is enriched (derived value + source hash written).
///   - The echo of that write is SKIPPED — no second write, so the loop can't run away.
///   - Repeatedly processing after a single edit performs exactly ONE write (bounded loop).
///   - Changing the source re-enriches; an unchanged source does not.
///   - The derived value is deterministic from the source.
/// </summary>
public class LoopSafeChangeFeedTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private const string SourceProperty = "text";
    private const string EnrichedProperty = "identicon";
    private const string HashProperty = "sourceHash";

    private readonly CosmosClient _client;
    private readonly string _databaseName = $"LoopSafeChangeFeedTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public LoopSafeChangeFeedTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync("Documents", "/id"));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers – mirror SourceHasher / DocumentEnricher in the pattern source.
    // -----------------------------------------------------------------------

    /// <summary>SHA-256 hex of the source value. The loop guard hashes the SOURCE only.</summary>
    private static string ComputeHash(string? sourceValue)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceValue ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>A deterministic derived value standing in for the identicon (derived from the hash).</summary>
    private static string Derive(string hash) => $"<svg data-hash=\"{hash[..8]}\"/>";

    /// <summary>
    /// Simulates one delivery of a document to the Change Feed processor. Returns true if it wrote
    /// back (source changed → enriched), false if it skipped (hash unchanged → echo of our write).
    /// </summary>
    private async Task<bool> ProcessOnceAsync(string id)
    {
        JObject doc = (await _container.ReadItemAsync<JObject>(id, new PartitionKey(id))).Resource;

        string newHash = ComputeHash(doc[SourceProperty]?.ToString());
        string? storedHash = doc[HashProperty]?.ToString();

        if (string.Equals(newHash, storedHash, StringComparison.Ordinal))
        {
            return false; // Echo of our own write — skip. This is what breaks the loop.
        }

        doc[EnrichedProperty] = Derive(newHash);
        doc[HashProperty] = newHash;
        await _container.ReplaceItemAsync(doc, id, new PartitionKey(id),
            new ItemRequestOptions { IfMatchEtag = doc["_etag"]?.ToString() });
        return true;
    }

    /// <summary>Writes only the source text, preserving other fields (read-modify-write).</summary>
    private async Task EditSourceAsync(string id, string text)
    {
        JObject doc;
        try
        {
            doc = (await _container.ReadItemAsync<JObject>(id, new PartitionKey(id))).Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            doc = new JObject { ["id"] = id };
        }

        doc[SourceProperty] = text;
        await _container.UpsertItemAsync(doc, new PartitionKey(id));
    }

    private async Task<JObject> ReadAsync(string id) =>
        (await _container.ReadItemAsync<JObject>(id, new PartitionKey(id))).Resource;

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NewDocument_IsEnriched_WithDerivedValueAndSourceHash()
    {
        string id = $"doc-{Guid.NewGuid():N}";
        await EditSourceAsync(id, "Hello, Cosmos!");

        bool wrote = await ProcessOnceAsync(id);

        Assert.True(wrote);
        JObject doc = await ReadAsync(id);
        Assert.Equal(ComputeHash("Hello, Cosmos!"), doc[HashProperty]?.ToString());
        Assert.False(string.IsNullOrEmpty(doc[EnrichedProperty]?.ToString()));
    }

    [Fact]
    public async Task Echo_OfEnrichmentWrite_IsSkipped()
    {
        string id = $"doc-{Guid.NewGuid():N}";
        await EditSourceAsync(id, "loop-safe");

        Assert.True(await ProcessOnceAsync(id));   // first delivery: enriched (a write)
        Assert.False(await ProcessOnceAsync(id));  // echo of that write: skipped — loop broken
    }

    [Fact]
    public async Task ProcessingRepeatedly_AfterOneEdit_WritesExactlyOnce()
    {
        string id = $"doc-{Guid.NewGuid():N}";
        await EditSourceAsync(id, "the loop must be bounded");

        int writes = 0;
        for (int i = 0; i < 5; i++)
        {
            if (await ProcessOnceAsync(id))
            {
                writes++;
            }
        }

        // Exactly one enrichment for one source edit; every later delivery is a skipped echo.
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task ChangingSource_ReEnriches_AndChangesDerivedValue()
    {
        string id = $"doc-{Guid.NewGuid():N}";
        await EditSourceAsync(id, "first");
        Assert.True(await ProcessOnceAsync(id));

        JObject before = await ReadAsync(id);
        string? hashBefore = before[HashProperty]?.ToString();
        string? derivedBefore = before[EnrichedProperty]?.ToString();

        await EditSourceAsync(id, "second");
        Assert.True(await ProcessOnceAsync(id));   // source changed → re-enriched

        JObject after = await ReadAsync(id);
        Assert.NotEqual(hashBefore, after[HashProperty]?.ToString());
        Assert.NotEqual(derivedBefore, after[EnrichedProperty]?.ToString());
    }

    [Fact]
    public async Task RewritingSameSource_DoesNotReEnrich()
    {
        string id = $"doc-{Guid.NewGuid():N}";
        await EditSourceAsync(id, "unchanged");
        Assert.True(await ProcessOnceAsync(id));

        // Writing the identical source again does not change the hash, so processing skips.
        await EditSourceAsync(id, "unchanged");
        Assert.False(await ProcessOnceAsync(id));
    }

    [Fact]
    public void Derivation_IsDeterministic_FromSource()
    {
        string hashA1 = ComputeHash("apple");
        string hashA2 = ComputeHash("apple");
        string hashB = ComputeHash("banana");

        Assert.Equal(hashA1, hashA2);
        Assert.Equal(Derive(hashA1), Derive(hashA2));
        Assert.NotEqual(hashA1, hashB);
        Assert.NotEqual(Derive(hashA1), Derive(hashB));
    }
}
