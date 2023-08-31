# Event Sourcing Pattern Demo

This folder contains an Azure Function that will simulate shopping cart events for an event sourcing pattern which appends events to Azure Cosmos DB. Use Program.cs to generate example events and send to an Azure function that deserializes and saves to Azure Cosmos DB.

## CosmosPatternsEventSourcingExample function

To run the function app for Bucketing pattern, you will need to have:

- [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)

## Confirm required tools are installed

Confirm you have the required versions of the tools installed for this demo.

First, check the .NET runtime with this command:

```bash
dotnet --list-runtimes
```

As you may have multiple versions of the runtime installed, make sure that .NET components with versions that start with 6.0 appear as part of the output.

Next, check the version of Azure Functions Core Tools with this command:

```bash
func --version
```

You should have a version 4._x_ installed. If you do not have this version installed, you will need to uninstall the older version and follow [these instructions for installing Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools).

## Create an Azure Cosmos DB for NoSQL account

This template will create an Azure Cosmos DB for NoSQL account with a database named `Sales` with a container named `CartEvents`. The partition key is set for `/CartId`. The data generator defaults to these values.

The suggested account name includes 'YOUR_SUFFIX'. Change that to a suffix to make your account name unique.

The Azure Cosmos DB for NoSQL account will automatically be created with the region of the selected resource group.

There is an option to enable the free tier. This is so that others can try this out with minimal costs to them.

**This link will work if this is a public repo.**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsolliancenet%2Fcosmos-db-nosql-modeling%2Fmain%2Fevent_sourcing%2Fcode%2Fazuredeploy.json)

**For the private repo**

1. [Create a custom template deployment](https://portal.azure.com/#create/Microsoft.Template).
2. Select **Build your own template in the editor**.
3. Copy the contents from [this template](azuredeploy.json) into the editor.
4. Select **Save**.

---

Once the template is loaded, populate the values:

- **Subscription** - Choose a subscription.
- **Resource group** - Choose a resource group.
- **Region** - Select a region for the instance.
- **Location** - Enter a location for the Azure Cosmos DB for NoSQL account. **Note**: By default, it is set to use the location of the resource group. If you need to change this value, you can find the supported regions for your subscription via:
  - [Azure CLI](https://learn.microsoft.com/cli/azure/account?view=azure-cli-latest#az-account-list-locations)
  - PowerShell: `Get-AzLocation | Where-Object {$_.Providers -contains "Microsoft.DocumentDB"} | Select location`
- **Account Name** - Replace `YOUR_SUFFIX` with a suffix to make your Azure Cosmos DB account name unique.
- **Database Name** - Set to the default **Sales**.
- **Cart Container Name** - This is the container partitioned by `/CartId`. Set to the default **CartEvents**.
- **Throughput** - Set to the default **400**.
- **Enable Free Tier** - This defaults to `false`. Set it to **true** if you want to use it as [the free tier account](https://learn.microsoft.com/azure/cosmos-db/free-tier).

Once those settings are set, select **Review + create**, then **Create**.

## Get Azure Cosmos DB connection information

You will need a connection string for the Azure Cosmos DB account.

1. Once the template deployment is complete, select **Go to resource group**.
2. Select the new Azure Cosmos DB for NoSQL account.
3. From the navigation, under **Settings**, select **Keys**. The values you need for the environment variables for the demo are here.

## Prepare the function app configuration

1. Add a file to the `Cosmos_Patterns_EventSourcing` folder called **local.settings.json** with the following contents:

    ```json
    {
        "IsEncrypted": false,
        "Values": {
            "AzureWebJobsStorage": "UseDevelopmentStorage=true",
            "FUNCTIONS_WORKER_RUNTIME": "dotnet",        
            "CosmosDBConnection" : "YOUR_PRIMARY_CONNECTION_STRING"
        }
    }
    ```

Make sure to replace `YOUR_PRIMARY_CONNECTION_STRING` with the `PRIMARY CONNECTION STRING` value noted earlier.

2. Edit **host.json** Set the `userAgentSuffix` to a value you prefer to use. This is used in tracking in Activity Monitor. See [host.json settings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-cosmosdb-v2?tabs=in-process%2Cextensionv4&pivots=programming-language-csharp#hostjson-settings) for more details.

## Run the demo

1. Start the function app to wait for HTTP calls. Each call should have a payload of a single CartEvent, then the function will save it to Cosmos DB.

```bash
func start
```

To trigger the function to generate events and send to the function, you can make HTTP calls with each CartEvent sent as JSON. Review and run Program.cs to see this in action.

Open a new terminal and run the included Console App (Program.cs) which generates simple shopping cart events:
```bash
dotnet run
```

## Querying the event source data
Once you have run [the demo](./code/setup.md) which generates data, you can run queries directly against the event source container by using **Data Explorer** in the Azure Portal.

1. In Azure Portal, browse to you Azure Cosmos DB resource.
2. Select **Data Explorer** in the left menu.
3. Select your container, then choose **New SQL Query**. 
![Screenshot of creating a SQL Query in Data Explorer within the Azure portal.](./images/data-explorer-create-new-query.png)

The most common query for this append-only store is to retrieve events for a specific `CartId`, ordered by `EventTimestamp`. In this case only the latest event for a cart is needed to know the last status and what products were in the cart.

The Console App (started with `dotnet run`) used in the demo will print out `CartId` values as it creates events.
```
HTTP function successful for event cart_created for cart 38f4687d-35f2-4933-aadd-8776f4134589.
```
Copy the query below and paste into the query pane in Data Explorer. **Replace the CartId value** with a GUID copied from the Console App program output.

```sql
SELECT *
FROM CartEvents c
WHERE c.CartId = "38f4687d-35f2-4933-aadd-8776f4134589"
ORDER BY c.EventTimestamp DESC
```

More complex queries can be run on the events container directly. Ideally, they will still use the partitionKey to optimize costs while the change feed is used to build other views when needed. One example is if the source application did not track the `productsInCart` information. In that case the product and quantities in the cart can be derived with a slightly more complex query. This query returns a record per product with the final quantity. It filters to a specific cart and also ignores the events that do not include a product, such as cart creation or purchase. You can test this in Data Explorer, but remember to replace the CartId value with one generated by running the demo.

```sql
SELECT c.CartId, c.UserId, c.Product,
    Sum(c.QuantityChange) as Quantity
FROM CartEvents c
WHERE c.CartId = "38f4687d-35f2-4933-aadd-8776f4134589"
    and IS_NULL(c.Product) = false 
GROUP BY c.CartId, c.UserId, c.Product
```