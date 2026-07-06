namespace Cosmos.VectorSearch;

/// <summary>Configuration for the vector-search sample.</summary>
public sealed class VectorSearchOptions
{
    public string DatabaseName { get; set; } = "MoviesDB";

    public string ContainerName { get; set; } = "Movies";

    /// <summary>The document property that stores the vector embedding.</summary>
    public string EmbeddingProperty { get; set; } = "embedding";

    /// <summary>The embedding model's output size (bge-micro-v2 produces 384-dim vectors).</summary>
    public int Dimensions { get; set; } = 384;
}
