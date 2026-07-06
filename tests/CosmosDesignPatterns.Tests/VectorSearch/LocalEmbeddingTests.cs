using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using SmartComponents.LocalEmbeddings;
using Xunit;

namespace CosmosDesignPatterns.Tests.VectorSearch;

// ---------------------------------------------------------------------------
// Model – a document carrying real 384-dimensional embeddings.
// ---------------------------------------------------------------------------

public class EmbeddedDoc
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class EmbeddedHit
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("score")]
    public double Score { get; set; }
}

/// <summary>
/// Tests that exercise the <b>real, local</b> embedding model end to end — with NO API key and NO
/// deployed model — so the "AI" part of the vector-search sample is genuinely covered by CI.
///
/// The embedding model (bge-micro-v2) is bundled in the SmartComponents.LocalEmbeddings NuGet
/// package and runs on the CPU, so these tests build and pass in a plain CI runner with no secrets.
/// This is the point worth proving: an LLM/embedding-powered app can be tested in CI/CD without
/// pre-provisioning a model or storing keys as secrets. Embeddings are deterministic, so semantic
/// assertions are stable.
/// </summary>
public class LocalEmbeddingTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"LocalEmbeddingTest-{Guid.NewGuid():N}";
    private readonly LocalEmbedder _embedder = new();
    private Container _container = default!;

    public LocalEmbeddingTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    private float[] Embed(string text) => _embedder.Embed(text).Values.ToArray();

    public async Task InitializeAsync()
    {
        Database db = await EmulatorFixture.WithRetryAsync(() => _client.CreateDatabaseIfNotExistsAsync(_databaseName));

        var properties = new ContainerProperties("Docs", "/id")
        {
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new Collection<Embedding>
            {
                new()
                {
                    Path = "/embedding",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 384, // bge-micro-v2 output size
                }
            }),
            IndexingPolicy = new IndexingPolicy
            {
                VectorIndexes = new Collection<VectorIndexPath>
                {
                    new() { Path = "/embedding", Type = VectorIndexType.DiskANN }
                }
            }
        };
        properties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/embedding/*" });

        _container = await EmulatorFixture.WithRetryAsync(() => db.CreateContainerIfNotExistsAsync(properties));

        // Three clearly-distinct topics, embedded with the real local model.
        (string Id, string Text)[] docs =
        {
            ("animals", "A playful kitten chases a ball of yarn across the living room floor."),
            ("finance", "The central bank raised interest rates to curb rising inflation this quarter."),
            ("space", "The rocket ignited its engines and carried the astronauts into orbit."),
        };
        foreach ((string id, string text) in docs)
        {
            await _container.UpsertItemAsync(
                new EmbeddedDoc { Id = id, Text = text, Embedding = Embed(text) },
                new PartitionKey(id));
        }
    }

    public async Task DisposeAsync()
    {
        _embedder.Dispose();
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    private static float Similarity(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }

    [Fact]
    public void Embedder_RunsLocally_And_RanksRelatedTextCloser()
    {
        // No Cosmos, no key, no cloud — just the local model. Related concepts must be closer.
        float[] cat = Embed("cat");
        float related = Similarity(cat, Embed("a small kitten"));
        float unrelated = Similarity(cat, Embed("quarterly financial report"));

        Assert.Equal(384, cat.Length);
        Assert.True(related > unrelated,
            $"Expected 'kitten' ({related:0.000}) to be closer to 'cat' than 'financial report' ({unrelated:0.000}).");
    }

    [Fact]
    public async Task SemanticSearch_WithRealEmbeddings_ReturnsMostRelevantTopicFirst()
    {
        // A query that shares no words with the stored text should still find the animals doc,
        // because the local model + Cosmos vector index rank by meaning.
        float[] query = Embed("a cute cat playing at home");

        var queryDef = new QueryDefinition(
                "SELECT TOP 3 c.id, VectorDistance(c.embedding, @q) AS score " +
                "FROM c ORDER BY VectorDistance(c.embedding, @q)")
            .WithParameter("@q", query);

        var hits = new List<EmbeddedHit>();
        using FeedIterator<EmbeddedHit> feed = _container.GetItemQueryIterator<EmbeddedHit>(queryDef);
        while (feed.HasMoreResults)
        {
            hits.AddRange((await feed.ReadNextAsync()).Resource);
        }

        Assert.Equal(3, hits.Count);
        Assert.Equal("animals", hits[0].Id);
    }
}
