---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Schema Versioning
urlFragment: schema-versioning
description: This example demonstrates how to implement a schema versioning making it possible to track the changes of schema through a schema tracking field.
---

# Azure Cosmos DB design pattern: Schema versioning

Schema versioning is used to track the schema changes of a document. The schema version can be tracked in a field in the document, such as `SchemaVersion`. If a document does not have the field present, it could be assumed to be the original version of the document.

Schema versioning allows NoSQL databases to evolve with applications, minimizing disruptions and preserving data integrity. However, managing schema versioning requires a clear strategy and processes to handle changes effectively.

This sample demonstrates:

- ✅ Using a data generator to generate data with an original schema, and data with a schema-version.
- ✅ Running a website to show data generated in Azure Cosmos DB.

## Common scenario

A major benefit of NoSQL databases is its ability to handle changes to schema. This is especially helpful in cases where an application has gone into production and it is necessary to adapt to changing data requirements. NoSQL databases like Azure Cosmos DB, not only make it possible to adapt to these changes but also to enable versioning of these changes by adding an additional property to track which version of the changes the data represents. This version can be used to handle and process the changing data at run-time.

## Sample implementation of schema versioning

In this scenario we have an online retailer, Wide World Importers, with data in Azure Cosmos DB for NoSQL. This is the initial cart object in their application.

```csharp
public class Cart
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int CustomerId { get; set; }
    public List<CartItem>? Items { get; set;}
}

public class CartItem {
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
}
```

When stored in Azure Cosmos DB for NoSQL, a cart would look like this:

```json
{
  "id": "194d7453-d9db-496b-834b-7b2db408e4be",
  "SessionId": "98f5621e-b1af-44f1-815c-f4aac728c4d4",
  "CustomerId": 741,
  "Items": [
    {
      "ProductName": "Product 23",
      "Quantity": 4
    },
    {
      "ProductName": "Product 16",
      "Quantity": 3
    }
  ]
}
```

This model was initially designed assuming products were ordered as-is without customizations. However, after feedback, they realized they needed to track special order details. It does not make sense to update all cart items with this feature, so adding a schema version property to the cart can be used to distinguish schema changes. The changes in the document would be handled at the application level.

This could be the updated class with schema versioning:

```csharp
public class CartWithVersion
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int CustomerId { get; set; }
    public List<CartItemWithSpecialOrder>? Items { get; set;}
    // Track the schema version
    public int SchemaVersion = 2;
}

public class CartItemWithSpecialOrder : CartItem {
    public bool IsSpecialOrder { get; set; } = false;
    public string? SpecialOrderNotes {  get; set; }
}
```

An updated cart in Azure Cosmos DB for NoSQL would look like this:

```json
{
  "SchemaVersion": 2,
  "id": "9baf08d2-e119-46a1-92d7-d94ee59d7270",
  "SessionId": "39306d1b-d8d8-424a-aa8b-800df123cb3c",
  "CustomerId": 827,
  "Items": [
    {
      "IsSpecialOrder": false,
      "SpecialOrderNotes": null,
      "ProductName": "Product 4",
      "Quantity": 2
    },
    {
      "IsSpecialOrder": true,
      "SpecialOrderNotes": "Special Order Details for Product 22",
      "ProductName": "Product 22",
      "Quantity": 2
    },
    {
      "IsSpecialOrder": true,
      "SpecialOrderNotes": "Special Order Details for Product 15",
      "ProductName": "Product 15",
      "Quantity": 3
    }
  ]
}
```

When it comes to data modeling, a schema version field in a JSON document can be incremented when schema changes happen. This could be used if data modeling happens with one team while development is handled separately. They could have a schema version document to help track these changes. In this example, it could look something like this:

---
**Filename**: schema.md

**Schema Updates**:

| Version | Notes |
|---------|-------|
| 2 | Added special order details to cart items |
| (null) | original release |

---

If you use a nullable type for the version, this will allow the developers to check for the presence of a value and act accordingly.

In this demo, `SchemaVersion` is treated as a nullable integer with the `int?` data type. The developers added a `HasSpecialOrders()` method to help determine whether to show the special order details. This is what the Cart class looks like on the website side:

```csharp
public class Cart
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public long CustomerId { get; set; }
    public List<CartItemWithSpecialOrder>? Items { get; set;}
    public int? SchemaVersion {get; set;}
    public bool HasSpecialOrders() {
        return this.Items.Where(x=>x.IsSpecialOrder == true).Count() > 0;
    }
}
```


## Try this implementation

You can run this sample locally or in GitHub Codespaces:

### GitHub Codespaces

Open the application code in GitHub Codespaces:

  [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fschema-versioning%2Fdevcontainer.json)


### Run locally

```bash
  git clone https://github.com/Azure-Samples/cosmos-db-design-patterns/
```

### Prerequisites

If running locally you will need to install .NET 8.

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download)

To confirm you have the required versions of the tools installed.

First, check the .NET runtime with this command. Make sure that .NET components with versions that start with 8.0 appear as part of the output:

```bash
dotnet --list-runtimes
```


## Set up application configuration files

You need to configure **two** application configuration files to run these demos.

1. Go to your resource group.

1. Select the Serverless Azure Cosmos DB for NoSQL account that you created for this repository.

1. From the navigation, under **Settings**, select **Keys**. The values you need for the application settings for the demo are here.

1. While on the Keys blade, make note of the `URI` and `PRIMARY KEY`. You will need these for the sections below.

1. In Codespace or locally, open the data-generator folder and add a new **appsettings.development.json** file with the following contents:

  ```json
    {
      "CosmosUri": "",
      "CosmosKey": "",
      "DatabaseName": "SchemaVersionDB",
      "ContainerName": "ShoppingCart",
      "PartitionKeyPath": "/id"
    }
  ```

1. Replace the `CosmosURI` and `CosmosKey` with the values from the Keys blade in the Azure Portal.

1. Save the file.

1. Open the website folder and add a new **appsettings.development.json** file with the following contents:

  ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "CosmosDb": {
        "CosmosUri": "",
        "CosmosKey": "",
        "DatabaseName": "SchemaVersionDB",
        "ContainerName": "ShoppingCart",
        "PartitionKeyPath": "/id"
      }
    }
  ```

1. Replace the `CosmosURI` and `CosmosKey` with the values from the Keys blade in the Azure Portal.

1. Save the file.


## Generate data

Navigate to the data-generator folder. Run the data generator to generate original carts and schema-versioned carts.

```bash
cd ./data-generator
dotnet run
```

The number of carts that you specify will be doubled. The generator generates the same number of original carts and versioned carts.

The output will look something like this:

```bash
This code will generate sample carts and create them in an Azure Cosmos DB for NoSQL account.
The primary key for this container will be /id.


Enter the database name [default:CartsDemo]:

Enter the container name [default:Carts]:

How many carts should be created?
3
Check Carts for new carts
Press Enter to exit.
```

## Run the website to show generated data

Run the website to display the carts.

```bash
cd ./website
dotnet run
```

Navigate to the URL displayed in the output. In the example below, the URL is shown as part of the `info` output, following the "Now listening on: " text.

![Screenshot of the 'dotnet run' output. The URL to navigate to is highlighted. In the screenshot, the URL is 'http://localhost:5279'.](./images/local-site-url.png)

The output will show a variety of randomly generated carts and include the schema version when populated. When a cart contains no special items, the Special Order Notes field will not appear in the cart table.

![Screenshot of the schema-versioned carts demo. The first cart shows 2 items with the fields Product Name and Quantity. The second cart shows 3 items with the fields for Schema Version, Product Name, Quantity, and Special Order Notes. The third cart shows 1 item with the fields for Schema Version, Product Name, Quantity, and Special Order Notes. The fourth cart shows 1 item with the fields for Schema Version, Product Name, and Quantity.](./images/schema-versioned-carts-website.png)

## Summary

Schema versioning is a valuable design pattern within Azure Cosmos DB. Azure Cosmos DB's schema-less nature aligns well with schema versioning. As applications evolve, schema changes can be seamlessly introduced without disrupting existing data. The ability to coexist with multiple schema versions guarantees backward compatibility. Applications can function with data in both old and new formats during the transition period.

In summary, schema versioning is a crucial design pattern for Azure Cosmos DB, promoting agility, compatibility, and robust data management within the dynamic landscape of modern applications.
