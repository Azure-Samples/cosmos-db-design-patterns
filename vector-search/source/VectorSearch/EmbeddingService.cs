using SmartComponents.LocalEmbeddings;

namespace Cosmos.VectorSearch;

/// <summary>
/// Turns text into a vector embedding using a small, local, offline model (bge-micro-v2, 384-dim,
/// bundled in the SmartComponents.LocalEmbeddings package — no API key or cloud call). This is the
/// <b>pluggable</b> part of the pattern: swap it for Azure OpenAI, or any embedding provider, and
/// everything else (the Cosmos DB vector index and queries) stays the same. The only rule is that
/// the same model must embed both the stored documents and the search query.
/// </summary>
public sealed class EmbeddingService : IDisposable
{
    private readonly LocalEmbedder _embedder = new();

    /// <summary>Embeds text into a 384-dimensional float vector.</summary>
    public float[] Embed(string text) => _embedder.Embed(text).Values.ToArray();

    public void Dispose() => _embedder.Dispose();
}
