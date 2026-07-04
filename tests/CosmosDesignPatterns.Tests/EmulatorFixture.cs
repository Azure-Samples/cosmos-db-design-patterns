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

    public void Dispose() => Client.Dispose();
}

/// <summary>xUnit collection that shares one <see cref="EmulatorFixture"/> across all test classes.</summary>
[CollectionDefinition("Emulator")]
public class EmulatorCollection : ICollectionFixture<EmulatorFixture> { }
