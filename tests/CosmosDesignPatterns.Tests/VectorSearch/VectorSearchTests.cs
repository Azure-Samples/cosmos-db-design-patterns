using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.VectorSearch;

// ---------------------------------------------------------------------------
// Model – a minimal item carrying a small vector, mirroring how the
// vector-search pattern stores an embedding on each document.
// ---------------------------------------------------------------------------

public class VectorDoc
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("vector")]
    public float[] Vector { get; set; } = Array.Empty<float>();
}

public class VectorHit
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("score")]
    public double Score { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Vector Search design pattern.
///
/// The pattern stores a vector embedding on each document and searches by meaning using a vector
/// index. These tests use small, deterministic vectors (so they assert exact ordering without an
/// embedding model) and verify the two Cosmos-specific pieces:
///   - a container created with a vector embedding policy + DiskANN vector index; and
///   - <c>VectorDistance()</c> with <c>ORDER BY VectorDistance(...)</c> returning nearest first,
///     including when combined with an ordinary metadata (category) filter.
/// </summary>
public class VectorSearchTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"VectorSearchTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public VectorSearchTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));

        var properties = new ContainerProperties("Vectors", "/id")
        {
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new Collection<Embedding>
            {
                new()
                {
                    Path = "/vector",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 3,
                }
            }),
            IndexingPolicy = new IndexingPolicy
            {
                VectorIndexes = new Collection<VectorIndexPath>
                {
                    new() { Path = "/vector", Type = VectorIndexType.DiskANN }
                }
            }
        };
        properties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/vector/*" });

        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync(properties));

        // Seed deterministic vectors: "a" and "c" point along +x (c is close to a), "b"/"d" don't.
        VectorDoc[] docs =
        {
            new() { Id = "a", Category = "X", Vector = [1f, 0f, 0f] },
            new() { Id = "b", Category = "Y", Vector = [0f, 1f, 0f] },
            new() { Id = "c", Category = "X", Vector = [0.9f, 0.1f, 0f] },
            new() { Id = "d", Category = "Y", Vector = [0f, 0f, 1f] },
        };
        foreach (VectorDoc doc in docs)
        {
            await _container.UpsertItemAsync(doc, new PartitionKey(doc.Id));
        }
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    private async Task<List<VectorHit>> SearchAsync(float[] query, string? category, int top)
    {
        string filter = category is null ? string.Empty : "WHERE c.category = @category ";
        var queryDef = new QueryDefinition(
                $"SELECT TOP @top c.id, c.category, VectorDistance(c.vector, @q) AS score " +
                $"FROM c {filter}ORDER BY VectorDistance(c.vector, @q)")
            .WithParameter("@top", top)
            .WithParameter("@q", query);
        if (category is not null)
        {
            queryDef.WithParameter("@category", category);
        }

        var results = new List<VectorHit>();
        using FeedIterator<VectorHit> feed = _container.GetItemQueryIterator<VectorHit>(queryDef);
        while (feed.HasMoreResults)
        {
            results.AddRange((await feed.ReadNextAsync()).Resource);
        }
        return results;
    }

    [Fact]
    public async Task VectorSearch_ReturnsNearestNeighborsFirst()
    {
        // Query along the +x axis: "a" (identical) should rank first, "c" (close) second.
        List<VectorHit> hits = await SearchAsync([1f, 0f, 0f], category: null, top: 4);

        Assert.Equal(4, hits.Count);
        Assert.Equal("a", hits[0].Id);
        Assert.Equal("c", hits[1].Id);
        // Similarity is highest for the identical vector.
        Assert.True(hits[0].Score >= hits[1].Score);
    }

    [Fact]
    public async Task VectorSearch_RespectsTopN()
    {
        List<VectorHit> hits = await SearchAsync([1f, 0f, 0f], category: null, top: 2);

        Assert.Equal(2, hits.Count);
        Assert.Equal("a", hits[0].Id);
    }

    [Fact]
    public async Task FilteredVectorSearch_CombinesCategoryFilterWithRanking()
    {
        // Restrict to category X: only "a" and "c" qualify, still nearest-first.
        List<VectorHit> hits = await SearchAsync([1f, 0f, 0f], category: "X", top: 5);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal("X", h.Category));
        Assert.Equal("a", hits[0].Id);
        Assert.Equal("c", hits[1].Id);
    }
}
