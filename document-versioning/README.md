---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Document Versioning
urlFragment: document-versioning
description: This example demonstrates how to implement document versioning in Azure Cosmos DB to track and manage historical versions of documents, such as orders in an eCommerce environment.
---

# Azure Cosmos DB design pattern: Document versioning

Document versioning is used to track the current version of a document and store historical documents in another collection. Where schema versioning tracks the schema changes, document versioning tracks the data changes. This pattern works well when there are few document versions to be tracked.

This sample demonstrates:

- ✅ It showcases how to implement document versioning in Azure Cosmos DB to track and manage the current and historical versions of documents.
- ✅  The example illustrates the separation of current document versions stored in a `CurrentOrderStatus` collection and historical document versions stored in a `HistoricalOrderStatus` collection.
- ✅ It highlights the integration of Azure Cosmos DB change feed with a function app to capture and copy the versioned documents to the historical collection, enabling efficient tracking and management of document versions.



## Common scenario

Some industries have regulations for data retention that require historical versions to be retained and tracked. Auditing and document control are other reasons for tracking versions. With document versioning, the current versions of documents are stored in a collection named to store the current documents. A second collection is named to store historical documents. This improves performance by allowing queries on current versions to be polled quickly without having to filter the historical results. The document versioning itself is handled at the application layer - outside of Azure Cosmos DB.

## Solution

In this example, we will explore document versioning using orders in an eCommerce environment.

Suppose we have this document:

```json
{
    "customerId": 10,
    "orderId": 1101,
    "status": "Submitted",
    "orderDetails": [
        [{"productName": "Product 1", "quantity": 1},
         {"productName": "Product 2", "quantity": 3}]
    ]
}
```

Now, suppose the customer had to cancel the order. The replacement document could look like this:

```json
{
    "customerId": 10,
    "orderId": 1101,
    "status": "Cancelled",
    "orderDetails": [
        [{"productName": "Product 1", "quantity": 1},
         {"productName": "Product 2", "quantity": 3}]
    ]
}
```

Looking at these documents, though, there is no easy way to tell which of these documents is the current document. By using document versioning, add a field to the document to track the version number. Update the current document in a `CurrentOrderStatus` container and add the change to the `HistoricalOrderStatus` container. While Azure Cosmos DB for NoSQL does not have a document versioning feature, you can build in the handling through an application. In [the demo](./code/setup.md), you can see how to implement the document versioning feature with the following components:

- A website that allows you to create orders and change the order status. The website updates the document version and saves the document to the current status container.
- A function app that reads the data for the Azure Cosmos DB change feed and copies the versioned documents to the historical status container

The demo website includes links to update the orders to the different statuses.

![Screenshot of the demo app - showing orders grouped by Submitted, Fulfilled, Delivered, and Cancelled statuses. The Submitted Orders have links for changing orders to Fulfilled or Cancelled. The Fulfilled orders have links to change orders to Delivered.](images/document-versioning-demo-2.png)

## Try this implementation

In order to run the demos, you will need:

- [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)

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

You should have installed a version that starts with `4.`. If you do not have a v4 version installed, you will need to uninstall the older version and follow [these instructions for installing Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools).

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview) with a [free Azure Cosmos DB account](https://learn.microsoft.com/azure/cosmos-db/try-free). (*This option doesn't require an Azure subscription, just a GitHub account.*)

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. Open the new account in the Azure portal and record the **URI** and **PRIMARY KEY** fields. These fields can be found in the **Keys** section of the account's page within the portal.

1. In the Data Explorer, create a new database and container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `Orders` |
    | **Container name** | `CurrentOrderStatus` |
    | **Partition key path** | `/CustomerId` |
    | **Throughput** | `400` (*Manual*) |

1. In the Data Explorer, create a second container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `Orders` |
    | **Container name** | `HistoricalOrderStatus` |
    | **Partition key path** | `/CustomerId` |
    | **Throughput** | `400` (*Manual*) |

## Set up environment variables

You need 2 environment variables to run these demos.

1. Once the template deployment is complete, select **Go to resource group**.
2. Select the new Azure Cosmos DB for NoSQL account.
3. From the navigation, under **Settings**, select **Keys**. The values you need for the environment variables for the demo are here.

Create 2 environment variables to run the demos:

- `COSMOS_ENDPOINT`: set to the `URI` value on the Azure Cosmos DB account Keys blade.
- `COSMOS_KEY`: set to the Read-Write `PRIMARY KEY` for the Azure Cosmos DB for NoSQL account

Create your environment variables with the following syntax:

Bash:

```bash
export COSMOS_ENDPOINT="YOUR_COSMOS_ENDPOINT"
export COSMOS_KEY="YOUR_COSMOS_KEY"
```


While on the Keys blade, make note of the `PRIMARY CONNECTION STRING`. You will need this for the Azure Function App.

## Prepare the function app configuration

1. Add a file to the `function-app` folder called **local.settings.json** with the following contents:

    ```json
    {
        "IsEncrypted": false,
        "Values": {
            "AzureWebJobsStorage": "UseDevelopmentStorage=false",
            "FUNCTIONS_WORKER_RUNTIME": "dotnet",        
            "CosmosDBConnection" : "YOUR_PRIMARY_CONNECTION_STRING"
        }
    }
    ```

    Make sure to replace `YOUR_PRIMARY_CONNECTION_STRING` with the `PRIMARY CONNECTION STRING` value noted earlier.

2. Edit **host.json** Set the `userAgentSuffix` to a value you prefer to use. This is used in tracking in Activity Monitor. See [host.json settings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-cosmosdb-v2?tabs=in-process%2Cextensionv4&pivots=programming-language-csharp#hostjson-settings) for more details.

## Run the demo locally

1. Switch to the `website` folder. Then start the website with:

    ```bash
    dotnet run
    ```

    Navigate to the URL displayed in the output. In the example below, the URL is shown as part of the `info` output, following the "Now listening on:" text.

    ![Screenshot of the 'dotnet run' output. The URL to navigate to is highlighted. In the screenshot, the URL is 'http://localhost:5183'.](images/local-site-url.png)

    **Do not doing anything on this website yet. Continue to the next step.**

1. At another command prompt, switch to the `function-app` folder. Then, run the function app with:

    ```bash
    func start
    ```

Now that you have the website and function app started, create 5-10 orders with the website.


This is what the website will look like when starting out:

![Screenshot of the Document Versioning Demo website. There is a form at the top labeled 'Create New Orders'. It has a field labeled 'Number to create', an input box, and a 'Submit' button. There are tables for Submitted Orders, Fulfilled Orders, Delivered Orders, and Cancelled Orders.](images/document-versioning-demo-1.png)

 The Create New Orders form will create orders without the DocumentVersion property. Enter a number in the **Number to create** text box, then select **Submit**. This is how the new order appears on the website:

![Screenshot of the Submitted Orders section with an order showing the Document Version of Submitted.](images/newly-submitted-order.png)

This is what the new order looks like in Azure Cosmos DB. Notice that the `DocumentVersion` property is absent.

![Screenshot of a query in Azure Data Explorer for the order above. The JSON result does not include the 'DocumentVersion' property.](images/newly-submitted-order-data-explorer.png)

The Azure Function is working directly with a `VersionedDocument` type, so it will carry the `DocumentVersion` field into the `HistoricalOrderStatus` container. For new documents, this will assume the DocumentVersion is 1 when it isn't specified.

Unversioned documents will still show as document version 1 due to the `VersionedOrder` C# class.

Select any of the links in the Links columns to change the status on the document.

- As you advance the status of the orders, notice that the Document Version field increments. The document version numbering is managed by the application, specifically in the `HandleVersioning()` function in the `OrderHelper` class in the `Services` folder.

    ![Screenshot of the Document Versioning Demo website. There are tables for Submitted Orders, Fulfilled Orders, Delivered Orders, and Cancelled Orders. Fulfilled Orders and Cancelled Orders show a Document Version of 2. Delivered Orders show a Document Version of 3.](images/document-versioning-demo-2.png)

- You can query the `CurrentOrderStatus` container in Data Explorer for the order number (`OrderId`) and Customer Id (`CustomerId`) and should only get back 1 document - the current document.

    In this example, the previously shown document was fulfilled. Notice in the Azure Data Explorer results that the `DocumentVersion` property is now a part of the document in `CurrentOrderStatus`.

    ![Screenshot of Azure Data Explorer with the document from the previous example. The query is querying for a specific OrderId and CustomerId in the CurrentOrderStatus container. The Status for this document is now Fulfilled. The DocumentVersion property appears at the top of the document and is now at 2.](images/newly-submitted-order-fulfilled-with-document-version.png)

- You can also query the `HistoricalOrderStatus` container for that order number and customer Id and get back the entire order status history.

    In this example, the previously shown document was fulfilled. Notice in the Azure Data Explorer results that the `DocumentVersion` property is now a part of the document in `CurrentOrderStatus`.

    ![Screenshot of Azure Data Explorer querying HistoricalOrderStatus with the OrderId and CustomerId from the previous example. The Status is now Fulfilled. The DocumentVersion property appears at the top of the document and is now at 2. There are 2 results in the results list.](images/newly-submitted-order-fulfilled-with-history.png)