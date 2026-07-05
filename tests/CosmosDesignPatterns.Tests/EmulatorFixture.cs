using System.Net;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace CosmosDesignPatterns.Tests;

/// <summary>
/// Shared fixture that creates a <see cref="CosmosClient"/> pointing at the Cosmos DB Linux emulator.
/// The emulator endpoint and key can be overridden with the COSMOS_ENDPOINT and COSMOS_KEY
/// environment variables so the same tests can be run against a real account in CI if needed.
/// </summary>
public class EmulatorFixture : IDisposable
{
    /// <summary>Well-known endpoint used by the Cosmos DB emulator.</summary>
    public const string DefaultEmulatorEndpoint = "https://localhost:8081";

    /// <summary>Well-known account key used by the Cosmos DB emulator.</summary>
    public const string DefaultEmulatorKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    public string Endpoint { get; }
    public string AccountKey { get; }
    public CosmosClient Client { get; }

    public EmulatorFixture()
    {
        Endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? DefaultEmulatorEndpoint;
        AccountKey = Environment.GetEnvironmentVariable("COSMOS_KEY") ?? DefaultEmulatorKey;

        // The emulator uses a self-signed certificate, so we bypass TLS validation.
        // This is safe because the emulator runs locally and is only used for testing.
        CosmosClientOptions options = new()
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }),
            ConnectionMode = ConnectionMode.Gateway
        };

        Client = new CosmosClient(Endpoint, AccountKey, options);
    }

    /// <summary>
    /// Runs a Cosmos DB operation, retrying transient <c>503 ServiceUnavailable</c> responses.
    /// The Cosmos DB Linux emulator sporadically returns 503 (substatus 1007) on database and
    /// container creation while its partition servers are still warming up, even after the
    /// gateway reports ready.  These errors are transient, so we back off and retry — this is
    /// the recommended way to work around emulator readiness flakiness in CI.
    /// </summary>
    public static async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, int maxAttempts = 15)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (CosmosException ex) when (
                ex.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < maxAttempts)
            {
                // Exponential-ish backoff capped at 5 s between attempts.
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 5)));
            }
        }
    }

    public void Dispose() => Client.Dispose();
}

/// <summary>xUnit collection that shares one <see cref="EmulatorFixture"/> across all test classes.</summary>
[CollectionDefinition("Emulator")]
public class EmulatorCollection : ICollectionFixture<EmulatorFixture> { }
