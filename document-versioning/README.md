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
- ✅ It highlights the integration of Azure Cosmos DB change feed with a Function App to capture and copy the versioned documents to the historical collection, enabling efficient tracking and management of document versions.

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
    {"productName": "Product 1", "quantity": 1},
    {"productName": "Product 2", "quantity": 3}
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
    {"productName": "Product 1", "quantity": 1},
    {"productName": "Product 2", "quantity": 3}
  ]
}
```

Looking at these documents, though, there is no easy way to tell which of these documents is the current document. By using document versioning, add a property to the document to track the version number. Update the current document in a `CurrentOrderStatus` container and add the change to the `HistoricalOrderStatus` container. In the two projects here, you can see how to implement the document versioning feature with the following components:

- A website that allows you to create orders and change the order status. The website updates the document version and saves the document to the current status container.
- A Function App that reads the data for the Azure Cosmos DB change feed and copies the versioned documents to the historical status container

The demo website includes links to update the orders to the different statuses.

![Screenshot of the demo app - showing orders grouped by Submitted, Fulfilled, Delivered, and Cancelled statuses. The Submitted Orders have links for changing orders to Fulfilled or Cancelled. The Fulfilled orders have links to change orders to Delivered.](images/document-versioning-demo-2.png)

## Try this implementation

### GitHub Codespaces

Open the application code in GitHub Codespaces:

  [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fdocument-versioning%2Fdevcontainer.json)

### Or Run locally

```bash
  git clone https://github.com/Azure-Samples/cosmos-db-design-patterns/
```

Directions installing pre-requisites to run locally and for cloning this repository using [Terminal or VS Code](../README.md?#getting-started)



## Set up application configuration files

You need to configure the application configuration file to run these demos.

1. Go to resource group.

1. Select the Serverless Azure Cosmos DB for NoSQL account that you created for this repository.

1. From the navigation, under **Settings**, select **Keys**. The values you need for the application settings for the demo are here.

While on the Keys blade, make note of the `URI`, `PRIMARy KEY` and `PRIMARY CONNECTION STRING`. You will need these for the sections below.

## Prepare the web app configuration

1. Open the website project and add a new **appsettings.development.json** file with the following contents:

```json
{
  "CosmosDb": {
    "CosmosUri": "",
    "CosmosKey": "",
    "Database": "DocumentVersionDB",
    "CurrentOrderContainer": "CurrentOrderStatus",
    "HistoricalOrderContainer": "HistoricalOrderStatus",
    "PartitionKey": "/CustomerId"
  }
}
```

1. Replace the `CosmosURI` and `CosmosKey` with the values from the Keys blade in the Azure Portal.

1. Save the file.


## Prepare the function app configuration

1. Open the application code. Add a file to the `function-app` folder called **local.settings.json** with the following contents:

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

1. Replace `YOUR_PRIMARY_CONNECTION_STRING` with the `PRIMARY CONNECTION STRING` value noted earlier.

1. Save the file.


## Run the demo

1. In Codespaces or locally, navigate to the `function-app` folder, start the Azure Function with:

```bash
  func start
```

1. Open a new Terminal.

1. Navigate to the `website` folder, start the website with:

```bash
  dotnet run
```

Navigate to the URL displayed in the output. In the example below, the URL is shown as part of the `info` output, following the "Now listening on:" text.

![Screenshot of the 'dotnet run' output. The URL to navigate to is highlighted. In the screenshot, the URL is 'http://localhost:5183'.](images/local-site-url.png)


1. With both the Azure Function and web app running, create 5-10 orders with the website.

This is what the website will look like when starting out:

![Screenshot of the Document Versioning Demo website. There is a form at the top labeled 'Create New Orders'. It has a field labeled 'Number to create', an input box, and a 'Submit' button. There are tables for Submitted Orders, Fulfilled Orders, Delivered Orders, and Cancelled Orders.](images/document-versioning-demo-1.png)

The Create New Orders form will create orders without the DocumentVersion property. Enter a number in the **Number to create** text box, then select **Submit**. This is how the new order appears on the website:

![Screenshot of the Submitted Orders section with an order showing the Document Version of Submitted.](images/newly-submitted-order.png)

This is what the new order looks like in Azure Cosmos DB. Notice that the `DocumentVersion` property is absent.

![Screenshot of a query in Azure Data Explorer for the order above. The JSON result does not include the 'DocumentVersion' property.](images/newly-submitted-order-data-explorer.png)

The Azure Function is working directly with a `VersionedDocument` type, so it will carry the `DocumentVersion` field into the `HistoricalOrderStatus` container. For new documents, this will assume the DocumentVersion is 1 when it isn't specified.

Unversioned documents will still show as document version 1 due to the `VersionedOrder` C# class.

Select any of the links in the Links columns to change the status on the document.

As you advance the status of the orders, notice that the Document Version field increments. The document version numbering is managed by the application, specifically in the `HandleVersioning()` function in the `OrderHelper` class in the `Services` folder.

![Screenshot of the Document Versioning Demo website. There are tables for Submitted Orders, Fulfilled Orders, Delivered Orders, and Cancelled Orders. Fulfilled Orders and Cancelled Orders show a Document Version of 2. Delivered Orders show a Document Version of 3.](images/document-versioning-demo-2.png)

You can query the `CurrentOrderStatus` container in Data Explorer for the order number (`OrderId`) and Customer Id (`CustomerId`) and should only get back 1 document - the current document.

In this example, the previously shown document was fulfilled. Notice in the Azure Data Explorer results that the `DocumentVersion` property is now a part of the document in `CurrentOrderStatus`.

![Screenshot of Azure Data Explorer with the document from the previous example. The query is querying for a specific OrderId and CustomerId in the CurrentOrderStatus container. The Status for this document is now Fulfilled. The DocumentVersion property appears at the top of the document and is now at 2.](images/newly-submitted-order-fulfilled-with-document-version.png)

You can also query the `HistoricalOrderStatus` container for that order number and customer Id and get back the entire order status history.

In this example, the previously shown document was fulfilled. Notice in the Azure Data Explorer results that the `DocumentVersion` property is now a part of the document in `CurrentOrderStatus`.

![Screenshot of Azure Data Explorer querying HistoricalOrderStatus with the OrderId and CustomerId from the previous example. The Status is now Fulfilled. The DocumentVersion property appears at the top of the document and is now at 2. There are 2 results in the results list.](images/newly-submitted-order-fulfilled-with-history.png)

## Summary

The **NoSQL Document Versioning** design pattern is used in NoSQL databases to manage different versions of documents efficiently. In scenarios where documents need to be updated frequently while retaining their historical states, this pattern ensures that changes are tracked and stored without overwriting the original data.
