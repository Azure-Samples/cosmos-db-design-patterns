using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MaterializedViews
{
    public class MaterializedViewProcessor
    {
        private readonly ILogger<MaterializedViewProcessor> _logger;

        public MaterializedViewProcessor(ILogger<MaterializedViewProcessor> logger)
        {
            _logger = logger;
        }

        [Function(nameof(MaterializedViewProcessor))]
        [CosmosDBOutput(
            databaseName: "MaterializedViewsDB",
            containerName: "SalesByProduct",
            Connection = "CosmosDBConnection",
            CreateIfNotExists = true,
            PartitionKey = "/Product")]
        public IReadOnlyList<SalesByProduct> Run(
            [CosmosDBTrigger(
                databaseName: "MaterializedViewsDB",
                containerName: "Sales",
                Connection = "CosmosDBConnection",
                LeaseContainerName = "leases",
                CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Sales> input)
        {
            var salesByProduct = new List<SalesByProduct>();

            if (input is null || input.Count == 0)
            {
                return salesByProduct;
            }

            _logger.LogInformation("Document count: {Count}", input.Count);

            foreach (Sales document in input)
            {
                salesByProduct.Add(new SalesByProduct(document));
            }

            return salesByProduct;
        }
    }
}

