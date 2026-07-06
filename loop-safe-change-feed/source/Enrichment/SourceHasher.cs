using System.Security.Cryptography;
using System.Text;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// Computes a stable hash of the source value. The hash is stored on the document and recomputed
/// every time the change feed delivers the document; comparing the two is what tells us whether
/// the source actually changed (enrich) or whether we're seeing the echo of our own write (skip).
/// </summary>
public static class SourceHasher
{
    /// <summary>Returns a lowercase hex SHA-256 of the source value (empty string when null).</summary>
    public static string Compute(string? sourceValue)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceValue ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
