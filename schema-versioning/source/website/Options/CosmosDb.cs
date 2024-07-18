namespace Versioning.Options
{
    public record CosmosDb
    {
        public required string CosmosUri { get; init; }
        public required string CosmosKey { get; init; }
        public required string DatabaseName { get; init; }
        public required string ContainerName { get; init; }
        public required string PartitionKeyPath { get; init; }

    };
}
