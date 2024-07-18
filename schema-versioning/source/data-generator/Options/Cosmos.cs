namespace data_generator.Options
{
    public record Cosmos
    {
        public string? CosmosUri { get; init; }
        public string? CosmosKey { get; init; }
        public string? DatabaseName { get; init; }
        public string? ContainerName { get; init; }
        public string? PartitionKeyPath { get; init; }
    }
}
