using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.DistributedCounter;

// ---------------------------------------------------------------------------
// Models – mirror the distributed-counter pattern source models
// ---------------------------------------------------------------------------

public enum CounterStatus
{
    Active = 0,
    Deleted = 1,
    Updating = 2,
    Pending = 3
}

/// <summary>The primary (logical) counter document.</summary>
public class PrimaryCounter
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    [JsonProperty("pk")]
    public string PK { get; set; } = string.Empty;
    [JsonProperty("name")]
    public string CounterName { get; set; } = string.Empty;
    [JsonProperty("startvalue")]
    public long StartValue { get; set; }
    [JsonProperty("status")]
    public CounterStatus Status { get; set; }
    [JsonProperty("docType")]
    public string DocType { get; set; } = "PrimaryCounter";
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}

/// <summary>One physical shard of the distributed counter.</summary>
public class DistributedCounter
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("pk")]
    public string ParentCounterId { get; set; } = string.Empty;
    [JsonProperty("countervalue")]
    public long Value { get; set; }
    [JsonProperty("status")]
    public CounterStatus Status { get; set; } = CounterStatus.Active;
    [JsonProperty("docType")]
    public string DocType { get; set; } = "DistributedCounter";
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Distributed Counter design pattern.
///
/// The pattern avoids hot-partition write contention by splitting a logical
/// counter into N physical "distributed counter" shards.  The true value is
/// the sum of all active shard values.
///
/// These tests verify:
///   - A primary counter and its shards can be stored and retrieved.
///   - The sum of shard values equals the initial counter value.
///   - Incrementing / decrementing a shard is reflected in the total.
///   - Splitting a shard into two maintains the total value.
/// </summary>
[Collection("Emulator")]
public class DistributedCounterTests : IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"DistributedCounterTest-{Guid.NewGuid():N}";
    private Container _countersContainer = default!;

    public DistributedCounterTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Partition key matches the distributed-counter pattern (/pk).
        _countersContainer = await db.CreateContainerIfNotExistsAsync("Counters", "/pk");
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a primary counter and N shards whose values sum to
    /// <paramref name="initialValue"/>.  Returns the primary counter id.
    /// </summary>
    private async Task<string> CreateCounterAsync(
        string counterName, long initialValue, int shardCount)
    {
        string counterId = Guid.NewGuid().ToString();
        long perShard = initialValue / shardCount;

        for (int i = 1; i <= shardCount; i++)
        {
            long shardValue = (i == shardCount)
                ? initialValue - (perShard * (shardCount - 1)) // last shard absorbs remainder
                : perShard;

            var shard = new DistributedCounter
            {
                ParentCounterId = counterId,
                Value = shardValue,
                Status = CounterStatus.Active
            };
            await _countersContainer.UpsertItemAsync(shard, new PartitionKey(counterId));
        }

        var primary = new PrimaryCounter
        {
            Id = counterId,
            PK = counterId,
            CounterName = counterName,
            StartValue = initialValue,
            Status = CounterStatus.Active
        };
        await _countersContainer.UpsertItemAsync(primary, new PartitionKey(counterId));

        return counterId;
    }

    /// <summary>Reads all active distributed-counter shards for a given primary counter.</summary>
    private async Task<List<DistributedCounter>> GetActiveShardsAsync(string counterId)
    {
        string sql = @"
            SELECT * FROM c
            WHERE c.pk = @pk
              AND c.docType = 'DistributedCounter'
              AND c.status = 0";          // 0 = Active

        var query = new QueryDefinition(sql).WithParameter("@pk", counterId);
        var shards = new List<DistributedCounter>();

        using FeedIterator<DistributedCounter> feed =
            _countersContainer.GetItemQueryIterator<DistributedCounter>(query);

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            shards.AddRange(page.Resource);
        }

        return shards;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateCounter_Stores_PrimaryCounter_And_Shards()
    {
        string counterId = await CreateCounterAsync("PageViews", initialValue: 1000, shardCount: 5);

        var primary = await _countersContainer.ReadItemAsync<PrimaryCounter>(
            counterId, new PartitionKey(counterId));

        Assert.Equal("PageViews", primary.Resource.CounterName);
        Assert.Equal(1000, primary.Resource.StartValue);
        Assert.Equal(CounterStatus.Active, primary.Resource.Status);

        var shards = await GetActiveShardsAsync(counterId);
        Assert.Equal(5, shards.Count);
    }

    [Fact]
    public async Task SumOfShards_Equals_InitialCounterValue()
    {
        const long initial = 300;
        const int shards = 3;
        string counterId = await CreateCounterAsync("Inventory", initialValue: initial, shardCount: shards);

        var activeShards = await GetActiveShardsAsync(counterId);
        long total = activeShards.Sum(s => s.Value);

        Assert.Equal(initial, total);
    }

    [Fact]
    public async Task IncrementShard_UpdatesShardValue_And_Total()
    {
        const long initial = 100;
        string counterId = await CreateCounterAsync("Downloads", initialValue: initial, shardCount: 2);

        var shards = await GetActiveShardsAsync(counterId);
        DistributedCounter shardToUpdate = shards.First();
        long originalValue = shardToUpdate.Value;

        // Increment by 50.
        shardToUpdate.Value += 50;
        await _countersContainer.UpsertItemAsync(shardToUpdate,
            new PartitionKey(shardToUpdate.ParentCounterId));

        var updatedShards = await GetActiveShardsAsync(counterId);
        long newTotal = updatedShards.Sum(s => s.Value);

        Assert.Equal(initial + 50, newTotal);
    }

    [Fact]
    public async Task DecrementShard_UpdatesShardValue_And_Total()
    {
        const long initial = 200;
        string counterId = await CreateCounterAsync("Tickets", initialValue: initial, shardCount: 4);

        var shards = await GetActiveShardsAsync(counterId);
        DistributedCounter shardToUpdate = shards.First();

        // Decrement by 25.
        shardToUpdate.Value -= 25;
        await _countersContainer.UpsertItemAsync(shardToUpdate,
            new PartitionKey(shardToUpdate.ParentCounterId));

        var updatedShards = await GetActiveShardsAsync(counterId);
        long newTotal = updatedShards.Sum(s => s.Value);

        Assert.Equal(initial - 25, newTotal);
    }

    [Fact]
    public async Task SplitShard_IntoTwo_MaintainsTotalValue()
    {
        const long initial = 100;
        string counterId = await CreateCounterAsync("SplitTest", initialValue: initial, shardCount: 1);

        var shards = await GetActiveShardsAsync(counterId);
        Assert.Single(shards);

        DistributedCounter original = shards.First();
        long half1 = original.Value / 2;
        long half2 = original.Value - half1;

        // Mark the original shard deleted and create two new shards.
        original.Status = CounterStatus.Deleted;
        await _countersContainer.UpsertItemAsync(original,
            new PartitionKey(original.ParentCounterId));

        var shard1 = new DistributedCounter { ParentCounterId = counterId, Value = half1 };
        var shard2 = new DistributedCounter { ParentCounterId = counterId, Value = half2 };
        await _countersContainer.UpsertItemAsync(shard1, new PartitionKey(counterId));
        await _countersContainer.UpsertItemAsync(shard2, new PartitionKey(counterId));

        var activeShards = await GetActiveShardsAsync(counterId);
        long total = activeShards.Sum(s => s.Value);

        Assert.Equal(2, activeShards.Count);
        Assert.Equal(initial, total);
    }

    [Fact]
    public async Task OddInitialValue_SplitAcrossShards_SumIsCorrect()
    {
        // 101 across 3 shards: 33, 33, 35 → total = 101
        const long initial = 101;
        const int shards = 3;
        string counterId = await CreateCounterAsync("OddValue", initialValue: initial, shardCount: shards);

        var activeShards = await GetActiveShardsAsync(counterId);
        long total = activeShards.Sum(s => s.Value);

        Assert.Equal(initial, total);
    }
}
