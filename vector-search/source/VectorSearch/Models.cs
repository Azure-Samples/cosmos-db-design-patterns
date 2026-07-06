using Newtonsoft.Json;

namespace Cosmos.VectorSearch;

/// <summary>
/// A catalog item (here, a movie). The <see cref="Embedding"/> is a 384-dimensional vector derived
/// from the title and plot; Azure Cosmos DB indexes it so similar items can be found by meaning.
/// </summary>
public sealed class Movie
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("plot")]
    public string Plot { get; set; } = string.Empty;

    [JsonProperty("genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonProperty("year")]
    public int Year { get; set; }

    /// <summary>The vector embedding of the item's text. Null until it has been computed.</summary>
    [JsonProperty("embedding")]
    public float[]? Embedding { get; set; }
}

/// <summary>One ranked hit from a vector search: the movie fields plus its similarity score.</summary>
public sealed class SearchResult
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("plot")]
    public string Plot { get; set; } = string.Empty;

    [JsonProperty("genre")]
    public string Genre { get; set; } = string.Empty;

    [JsonProperty("year")]
    public int Year { get; set; }

    /// <summary>Cosine similarity to the query (higher is more similar; 1.0 is identical).</summary>
    [JsonProperty("score")]
    public double Score { get; set; }
}
