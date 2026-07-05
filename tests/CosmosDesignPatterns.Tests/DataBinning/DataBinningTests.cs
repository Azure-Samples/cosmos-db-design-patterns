using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.DataBinning;

// ---------------------------------------------------------------------------
// Models – mirror the data-binning pattern source models
// ---------------------------------------------------------------------------

/// <summary>One raw sensor reading included in a summary bin.</summary>
public class BinReading
{
    [JsonProperty("eventTimestamp")]
    public string? EventTimestamp { get; set; }
    [JsonProperty("temperature")]
    public double Temperature { get; set; }
}

/// <summary>
/// Aggregated summary document written once per time-window per device.
/// Partition key is DeviceId.
/// </summary>
public class SummarySensorEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonProperty("DeviceId")]
    public string? DeviceId { get; set; }
    [JsonProperty("numberOfReadings")]
    public int NumberOfReadings { get; set; }
    [JsonProperty("avgTemperature")]
    public double AvgTemperature { get; set; }
    [JsonProperty("minTemperature")]
    public double MinTemperature { get; set; }
    [JsonProperty("maxTemperature")]
    public double MaxTemperature { get; set; }
    [JsonProperty("readings")]
    public BinReading[]? Readings { get; set; }
    [JsonProperty("eventTimestamp")]
    public string? EventTimestamp { get; set; }
    [JsonProperty("receivedTimestamp")]
    public string? ReceivedTimestamp { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Data Binning design pattern.
///
/// The pattern collects raw sensor events over a time window and persists a
/// single summary document (bin) containing aggregated statistics
/// (count, average, min, max) plus the individual readings.  This reduces
/// document count and write RUs compared to storing every raw event.
///
/// These tests verify:
///   - A summary bin can be stored and retrieved from Cosmos DB.
///   - Aggregated statistics (avg, min, max) match the expected values.
///   - Multiple bins for the same device can coexist.
///   - Bins for different devices are kept isolated.
///   - A query by DeviceId returns only that device's summaries.
/// </summary>
public class DataBinningTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"DataBinningTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public DataBinningTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));
        // Partition key matches the data-binning pattern (/DeviceId).
        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync("SensorBins", "/DeviceId"));
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static SummarySensorEvent BuildBin(string deviceId, double[] temps)
    {
        var readings = temps.Select(t => new BinReading
        {
            EventTimestamp = DateTime.UtcNow.ToString("o"),
            Temperature = t
        }).ToArray();

        return new SummarySensorEvent
        {
            DeviceId = deviceId,
            NumberOfReadings = readings.Length,
            AvgTemperature = readings.Average(r => r.Temperature),
            MinTemperature = readings.Min(r => r.Temperature),
            MaxTemperature = readings.Max(r => r.Temperature),
            Readings = readings,
            EventTimestamp = DateTime.UtcNow.ToString("o"),
            ReceivedTimestamp = DateTime.UtcNow.ToString("o")
        };
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_SummarySensorEvent_Succeeds()
    {
        var bin = BuildBin("device-1", [68.0, 70.5, 72.0]);

        await _container.UpsertItemAsync(bin, new PartitionKey(bin.DeviceId));

        var result = await _container.ReadItemAsync<SummarySensorEvent>(
            bin.Id, new PartitionKey(bin.DeviceId));

        Assert.Equal(bin.DeviceId, result.Resource.DeviceId);
        Assert.Equal(3, result.Resource.NumberOfReadings);
    }

    [Fact]
    public async Task SummarySensorEvent_HasCorrectAggregatedValues()
    {
        double[] temps = [65.0, 70.0, 75.0, 80.0];
        var bin = BuildBin("device-agg", temps);

        await _container.UpsertItemAsync(bin, new PartitionKey(bin.DeviceId));

        var result = await _container.ReadItemAsync<SummarySensorEvent>(
            bin.Id, new PartitionKey(bin.DeviceId));

        Assert.Equal(65.0, result.Resource.MinTemperature, precision: 5);
        Assert.Equal(80.0, result.Resource.MaxTemperature, precision: 5);
        Assert.Equal(72.5, result.Resource.AvgTemperature, precision: 5);
        Assert.Equal(4, result.Resource.NumberOfReadings);
    }

    [Fact]
    public async Task MultipleBins_ForSameDevice_CanCoexist()
    {
        string deviceId = "device-multi";
        var bin1 = BuildBin(deviceId, [68.0, 69.0]);
        var bin2 = BuildBin(deviceId, [71.0, 72.0]);

        await _container.UpsertItemAsync(bin1, new PartitionKey(deviceId));
        await _container.UpsertItemAsync(bin2, new PartitionKey(deviceId));

        var r1 = await _container.ReadItemAsync<SummarySensorEvent>(bin1.Id, new PartitionKey(deviceId));
        var r2 = await _container.ReadItemAsync<SummarySensorEvent>(bin2.Id, new PartitionKey(deviceId));

        Assert.Equal(2, r1.Resource.NumberOfReadings);
        Assert.Equal(2, r2.Resource.NumberOfReadings);
        Assert.NotEqual(r1.Resource.Id, r2.Resource.Id);
    }

    [Fact]
    public async Task DifferentDevices_HaveIsolatedBins()
    {
        var binA = BuildBin("device-A", [65.0]);
        var binB = BuildBin("device-B", [99.0]);

        await _container.UpsertItemAsync(binA, new PartitionKey(binA.DeviceId));
        await _container.UpsertItemAsync(binB, new PartitionKey(binB.DeviceId));

        var resultA = await _container.ReadItemAsync<SummarySensorEvent>(binA.Id, new PartitionKey("device-A"));
        var resultB = await _container.ReadItemAsync<SummarySensorEvent>(binB.Id, new PartitionKey("device-B"));

        Assert.Equal(65.0, resultA.Resource.AvgTemperature, precision: 5);
        Assert.Equal(99.0, resultB.Resource.AvgTemperature, precision: 5);
    }

    [Fact]
    public async Task QueryByDeviceId_ReturnsSummariesForThatDevice()
    {
        string targetDevice = "device-query-target";
        var bin1 = BuildBin(targetDevice, [70.0, 71.0]);
        var bin2 = BuildBin(targetDevice, [72.0, 73.0]);
        var other = BuildBin("device-other", [50.0]);

        await _container.UpsertItemAsync(bin1, new PartitionKey(targetDevice));
        await _container.UpsertItemAsync(bin2, new PartitionKey(targetDevice));
        await _container.UpsertItemAsync(other, new PartitionKey(other.DeviceId));

        string sql = "SELECT * FROM c WHERE c.DeviceId = @deviceId";
        var query = new QueryDefinition(sql).WithParameter("@deviceId", targetDevice);
        var results = new List<SummarySensorEvent>();

        using FeedIterator<SummarySensorEvent> feed =
            _container.GetItemQueryIterator<SummarySensorEvent>(query);

        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(targetDevice, r.DeviceId));
    }
}
