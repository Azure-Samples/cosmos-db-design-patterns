using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;

namespace Cosmos.VectorSearch;

/// <summary>
/// Stores movies in Azure Cosmos DB and finds them by <b>meaning</b> using a vector index.
///
/// The two Cosmos-specific pieces of the pattern are:
/// <list type="number">
///   <item>a <b>vector embedding policy</b> + <b>vector index</b> on the embedding property, set
///   when the container is created (see <see cref="EnsureContainerAsync"/>); and</item>
///   <item>the <c>VectorDistance()</c> function in a query, with <c>ORDER BY VectorDistance(...)</c>
///   returning the nearest matches first (see <see cref="SearchAsync"/>).</item>
/// </list>
/// A big Cosmos advantage: you can combine an ordinary <c>WHERE</c> filter (here, on genre) with
/// vector ranking in a single query — no separate search service required.
/// </summary>
public sealed class MovieVectorStore
{
    private readonly Container _container;
    private readonly EmbeddingService _embeddings;
    private readonly VectorSearchOptions _options;

    public MovieVectorStore(CosmosClient client, EmbeddingService embeddings, VectorSearchOptions options)
    {
        _embeddings = embeddings;
        _options = options;
        _container = client.GetContainer(options.DatabaseName, options.ContainerName);
    }

    /// <summary>
    /// Creates the database and a container whose embedding property is covered by a vector index.
    /// In Azure these are pre-created by azd; locally against the emulator this creates them on
    /// first run. The vector policy and index can only be set at creation time.
    /// </summary>
    public static async Task EnsureContainerAsync(CosmosClient client, VectorSearchOptions options, CancellationToken cancellationToken = default)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(options.DatabaseName, cancellationToken: cancellationToken);

        string embeddingPath = "/" + options.EmbeddingProperty;

        var containerProperties = new ContainerProperties(options.ContainerName, "/id")
        {
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new Collection<Embedding>
            {
                new()
                {
                    Path = embeddingPath,
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = options.Dimensions,
                }
            }),
            IndexingPolicy = new IndexingPolicy
            {
                VectorIndexes = new Collection<VectorIndexPath>
                {
                    new() { Path = embeddingPath, Type = VectorIndexType.DiskANN }
                }
            }
        };

        // Don't include the (large) embedding array in the normal index — the vector index covers it.
        containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = embeddingPath + "/*" });

        await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken);
    }

    /// <summary>Computes the movie's embedding (from title + plot) if needed, then upserts it.</summary>
    public async Task UpsertAsync(Movie movie, CancellationToken cancellationToken = default)
    {
        movie.Embedding ??= _embeddings.Embed($"{movie.Title}. {movie.Plot}");
        await _container.UpsertItemAsync(movie, new PartitionKey(movie.Id), cancellationToken: cancellationToken);
    }

    /// <summary>Returns the number of movies currently stored.</summary>
    public async Task<int> CountAsync()
    {
        using FeedIterator<int> feed = _container.GetItemQueryIterator<int>("SELECT VALUE COUNT(1) FROM c");
        return feed.HasMoreResults ? (await feed.ReadNextAsync()).First() : 0;
    }

    /// <summary>
    /// Finds the movies most similar in meaning to <paramref name="query"/>. When
    /// <paramref name="genre"/> is supplied, the search is restricted to that genre (a metadata
    /// filter combined with vector ranking in a single query).
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, string? genre = null, int top = 5)
    {
        float[] queryVector = _embeddings.Embed(query);
        string embeddingRef = "c." + _options.EmbeddingProperty;
        string filter = string.IsNullOrWhiteSpace(genre) ? string.Empty : "WHERE c.genre = @genre ";

        var queryDefinition = new QueryDefinition(
                $"SELECT TOP @top c.id, c.title, c.plot, c.genre, c.year, " +
                $"VectorDistance({embeddingRef}, @vector) AS score " +
                $"FROM c {filter}ORDER BY VectorDistance({embeddingRef}, @vector)")
            .WithParameter("@top", top)
            .WithParameter("@vector", queryVector);

        if (!string.IsNullOrWhiteSpace(genre))
        {
            queryDefinition.WithParameter("@genre", genre);
        }

        var results = new List<SearchResult>();
        using FeedIterator<SearchResult> feed = _container.GetItemQueryIterator<SearchResult>(queryDefinition);
        while (feed.HasMoreResults)
        {
            foreach (SearchResult result in await feed.ReadNextAsync())
            {
                results.Add(result);
            }
        }

        return results;
    }
}
