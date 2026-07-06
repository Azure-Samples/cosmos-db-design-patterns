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

## Web front end

The website reads each cart's `SchemaVersion` and renders it accordingly, so original (v1) and newer (v2, with special-order items) documents **coexist in the same container**:

![Schema Versioning web front end showing v1 and v2 shopping carts side by side](images/schema-versioning-web.png)

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


## Getting the code

### Using Terminal or VS Code

Directions for installing pre-requisites and cloning this repository are in the [root README](../README.md#getting-started).

## Set up application configuration

This sample has two apps: a **data-generator** (console) and a **website**. Both read their Azure Cosmos DB settings from configuration — see [Configuration and authentication](../README.md#configuration-and-authentication) in the root README.

The **website defaults to the local emulator** (`https://localhost:8081`) when nothing is configured, so it runs with zero setup. For the **data-generator**, add an `appsettings.development.json` file (git-ignored) to its folder with the emulator endpoint and key:

```json
{
  "CosmosUri": "https://localhost:8081",
  "CosmosKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
  "DatabaseName": "SchemaVersionDB",
  "ContainerName": "ShoppingCart",
  "PartitionKeyPath": "/id"
}
```

To use your own Azure Cosmos DB account with keyless (RBAC) authentication instead, set `CosmosUri` to your account endpoint and leave `CosmosKey` empty — the apps then use `DefaultAzureCredential`. Grant your identity the **Cosmos DB Built-in Data Contributor** role first. (In the website, these settings bind under a `CosmosDb` section — see the root configuration table.)


## Run the demo locally

Start the local emulator first (see the [root README](../README.md#run-locally-with-the-emulator-default)):

```bash
docker compose up -d
```

### Generate data

Navigate to the data-generator folder. Run the data generator to generate original carts and schema-versioned carts.

```bash
cd source/data-generator
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

### Run the website to show generated data

Run the website to display the carts.

```bash
cd source/website
dotnet run
```

Open the URL it prints. The output will show a variety of randomly generated carts and include the schema version when populated. When a cart contains no special items, the Special Order Notes field will not appear in the cart table.

![Screenshot of the schema-versioned carts demo. The first cart shows 2 items with the fields Product Name and Quantity. The second cart shows 3 items with the fields for Schema Version, Product Name, Quantity, and Special Order Notes. The third cart shows 1 item with the fields for Schema Version, Product Name, Quantity, and Special Order Notes. The fourth cart shows 1 item with the fields for Schema Version, Product Name, and Quantity.](./images/schema-versioned-carts-website.png)

## (Optional) Deploy and run in Azure with `azd`

The steps above are the **all-local** way to run the sample. If you'd rather run the **all-Azure** way — the sample deployed and running in Azure — this pattern includes an [Azure Developer CLI (`azd`)](https://aka.ms/azd) template. Running locally is unchanged; the deployment files (`azure.yaml`, `infra/`) have no effect unless you run `azd up`.

It provisions and deploys, intentionally minimal and cheap:

- An **App Service** web app (Basic **B1**).
- A **serverless** Azure Cosmos DB account with local (key) authentication **disabled**.
- The web app reaches Cosmos DB **keyless**, via a **user-assigned managed identity** — no keys or connection strings are stored anywhere.

### Deploy

From the `schema-versioning` folder:

```bash
azd up
```

`azd` prompts for an environment name, subscription, and location, then provisions the resources and deploys the site. When it finishes it prints the site URL — open it to see the schema-versioned carts exactly as in the local walkthrough above.

### Clean up

```bash
azd down
```

## Summary

Schema versioning is a valuable design pattern within Azure Cosmos DB. Azure Cosmos DB's schema-less nature aligns well with schema versioning. As applications evolve, schema changes can be seamlessly introduced without disrupting existing data. The ability to coexist with multiple schema versions guarantees backward compatibility. Applications can function with data in both old and new formats during the transition period.

In summary, schema versioning is a crucial design pattern for Azure Cosmos DB, promoting agility, compatibility, and robust data management within the dynamic landscape of modern applications.
