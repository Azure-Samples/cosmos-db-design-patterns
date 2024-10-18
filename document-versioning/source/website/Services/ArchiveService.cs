using Microsoft.Azure.Cosmos;
using Models;


namespace Services
{
    public class ArchiveService
    {
        
        private readonly CosmosDb cosmosDb;
        private readonly ChangeFeedProcessor? changeFeedProcessor;

        public ArchiveService(CosmosDb cosmosDb)
        {
            this.cosmosDb = cosmosDb;

            changeFeedProcessor = StartChangeFeedProcessorAsync(cosmosDb.OrderContainer, cosmosDb.LeasesContainer).Result;
        }

        

        //create a function to start Cosmos DB Change Feed Processor
        private async Task<ChangeFeedProcessor> StartChangeFeedProcessorAsync(Container monitoredContainer, Container leasesContainer)
        {
            // Create an instance of the Change Feed Processor
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<VersionedOrder>("DocumentVersioningProcessor", HandleChangesAsync)
                .WithInstanceName("DocumentVersioningProcessor")
                .WithLeaseContainer(leasesContainer)
                .Build();

            // Start the Change Feed Processor
            await changeFeedProcessor.StartAsync();

            return changeFeedProcessor;
        }

        //Implement the HandleChangesAsync method
        private async Task HandleChangesAsync(IReadOnlyCollection<VersionedOrder> changes, CancellationToken cancellationToken)
        {
            foreach (VersionedOrder versionedOrder in changes)
            {

                Console.WriteLine($"Change detected: {versionedOrder.OrderId}");
                // new id for the historical collection to preserve the history rather than overwrite it
                versionedOrder.id = System.Guid.NewGuid().ToString();

                // Archive the document
                await cosmosDb.HistoryContainer.CreateItemAsync<VersionedOrder>(versionedOrder, new PartitionKey(versionedOrder.CustomerId));
            }
        }

    }
}
