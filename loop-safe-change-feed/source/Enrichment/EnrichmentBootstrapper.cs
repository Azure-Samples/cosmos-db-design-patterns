using Microsoft.Azure.Cosmos;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// Creates the database, the documents container, and the change-feed lease container when they
/// don't already exist. In Azure these are pre-created by the azd/Bicep deployment; locally
/// against the emulator this creates them on first run so the sample works with zero setup.
/// </summary>
public static class EnrichmentBootstrapper
{
    public static async Task EnsureContainersAsync(
        CosmosClient client,
        EnrichmentOptions options,
        CancellationToken cancellationToken = default)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(
            options.DatabaseName, cancellationToken: cancellationToken);

        await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.ContainerName,
            PartitionKeyPath = "/id",
        }, cancellationToken: cancellationToken);

        // The change feed processor stores its position (leases) here. Partitioned on /id like any
        // Cosmos container; the processor manages the documents inside it.
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.LeaseContainerName,
            PartitionKeyPath = "/id",
        }, cancellationToken: cancellationToken);
    }
}
