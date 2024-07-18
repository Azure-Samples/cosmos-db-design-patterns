namespace Options
{
    public record CosmosDb
    {
        public required string CosmosUri { get; init; }
        public required string CosmosKey { get; init; }
        public required string Database { get; init; }
        public required string CurrentOrderContainer { get; init; }
        public required string HistoricalOrderContainer { get; init; }
        public required string PartitionKey { get; init; }

    };
}
