---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Attribute array
urlFragment: attribute-array
description: Review this example of using attribute array to efficiently query a variable quantity of attributes.
---

# Azure Cosmos DB design pattern: Attribute array

The attribute array pattern is a unique design pattern specific to JSON-based NoSQL databases where multiple name/value pairs containing similar data are modeled as arrays within a document. The alternative would model as these individual name/value pairs on the parent object.

There are two main advantages to this pattern. First is it can simplify query construction making queries more concise and less prone to bugs. Second is it is more efficient to index, reducing cost and improving performance.

This sample demonstrates:

- ✅ Creation of documents based on property and attribute array data patterns.
- ✅ Querying both property and array objects in a container.

## Common scenario

A common scenario for using a NoSQL attribute array design pattern is when you have entities with a large number of similar attributes. It can be even more useful if the properties are undefined when null or otherwise change frequently and want to avoid schema changes or migrations.

Let's say you're developing an e-commerce platform where sellers list their products. Each product can have various attributes such as size, color, brand, price, description, and more. Different sellers may have different attributes for their products. Some sellers might define attributes when null or zero, others leave properties undefined when there is no value to store.

In a relational database, you might create a One:Many relationship between two tables to model this, or possibly use a single table with columns for each attribute. However, in this case, the number and type of attributes can vary greatly between different products and sellers, making it difficult to define a fixed schema.

NoSQL databases differ from relational databases in that relationships are materialized with the data itself inside a document. Using a NoSQL database with an attribute array design pattern, the product attributes are stored as an array in the document. Each attribute is represented as a key-value pair, where the key is the attribute name (for example, "color") and the value is the corresponding attribute value (for example, "red"). You can also model arrays of objects using this pattern and use any shared name/value pairs to filter for specific data within the array. For example, you can have an array of Sizes, and name/value pairs of Count and Price for each Size.

With this design pattern, you can easily accommodate the varying attributes of different products and sellers. Sellers can add or remove attributes as needed without schema modifications or complex migrations. Queries can be performed on specific attribute values, and you can even index certain attributes for efficient searching.

Overall, the NoSQL attribute array design pattern is suitable when you have entities with dynamic and variable attributes. This pattern allows for flexibility, scalability, and easy adaptability to changing requirements.

## Sample implementation

Here is a simple scenario where you can create an attribute array to capture a variable number of attributes for each item.

### Products with sizes

Products like shirts and sweaters tend to have multiple sizes that may be in inventory. To accommodate the various sizes, a naive data model with each size count might look like this:

```csharp
record Product(
    string Id,
    string ProductId,
    string Name,
    string Category,
    decimal Price,
    int SizeSmall,
    int SizeMedium,
    int SizeLarge
);
```

The object JSON saved to Azure Cosmos DB would look like the following example:

```json
{
  "id": "89e89f1a-3c9d-c043-7c3f-8522d6a1ef01",
  "productId": "89e89f1a-3c9d-c043-7c3f-8522d6a1ef01",
  "name": "Sleek Fresh Shoes",
  "category": "Computers, Outdoors & Shoes",
  "price": 895.37,
  "sizeSmall": 24,
  "sizeMedium": 61,
  "sizeLarge": 51
}
```

If you wished to find all of the sizes (small, medium, or large) with a quantity of greater than 75, you would need to use multiple `OR` expressions like in this query:

```sql
SELECT VALUE p FROM products p WHERE p.sizeSmall >= 75 OR p.sizeMedium >= 75 OR p.sizeLarge >= 75
```

An attribute array-based approach would create a list property where the sizes are in a collection:

```csharp
record Product(
    string Id,
    string ProductId,
    string Name,
    string Category,
    decimal Price,
    IList<ProductSize> Sizes
);

record ProductSize(
    string Size,
    int Count
);
```

The object JSON saved to Azure Cosmos DB would look like the following example:

```json
{
  "id": "76841ca4-679b-5cd9-406f-28216d30d71e",
  "productId": "76841ca4-679b-5cd9-406f-28216d30d71e",
  "name": "Practical Metal Sausages",
  "category": "Jewelery",
  "price": 480.70,
  "sizes": [
    {
      "name": "Small",
      "count": 24
    },
    {
      "name": "Medium",
      "count": 96
    },
    {
      "name": "Large",
      "count": 80
    }
  ]
}
```

This pattern results in a simpler, more flexible query using a `JOIN` expression:

```sql
SELECT p.name, s.size, s.count FROM products p JOIN s IN p.sizes WHERE s.count >= 75
```

If a user adds new sizes or even removes them. The same query will run unmodified, future-proofing your design and avoiding potential bugs.

## Try this implementation

In order to run the demos, you will need:

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)

## Confirm required tools are installed

Confirm you have the required versions of the tools installed for this demo.

First, check the .NET runtime with this command:

```bash
dotnet --list-runtimes
```

As you may have multiple versions of the runtime installed, make sure that .NET components with versions that start with 6.0 appear as part of the output.

## Getting the code

### **Clone the Repository to Your Local Computer:**

**Using the Terminal:**

- Open the terminal on your computer.
- Navigate to the directory where you want to clone the repository.
- Type `git clone https://github.com/Azure-Samples/cosmos-db-design-patterns.git` and press enter.
- The repository will be cloned to your local machine.

**Using Visual Studio Code:**

- Open Visual Studio Code.
- Click on the **Source Control** icon in the left sidebar.
- Click on the **Clone Repository** button at the top of the Source Control panel.
- Paste `https://github.com/Azure-Samples/cosmos-db-design-patterns.git` into the text field and press enter.
- Select a directory where you want to clone the repository.
- The repository will be cloned to your local machine.

### **GitHub Codespaces**

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview)

- Open the application code in a GitHub Codespace:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fattribute-array%2Fdevcontainer.json)

## Create an Azure Cosmos DB for NoSQL account

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. In the Data Explorer, create a new database named **CosmosPatterns** with shared autoscale throughput:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Throughput** | `1000` (*Autoscale*) |

**Note:** We are using shared database throughput because it can scale down to 100 RU/s when not running. This is the most cost efficient if running in a paid subscription and not using Free Tier.

1. Create a container **AttributeArrays** with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `AttributeArrays` |
    | **Partition key path** | `/productId` |

## Get Azure Cosmos DB connection information

You will need connection details for the Azure Cosmos DB account.

1. Select the new Azure Cosmos DB for NoSQL account.

1. Open the Keys blade, click the Eye icon to view the `PRIMARY KEY`. Keep this and the `URI` handy. You will need these for the next step.

## Prepare the app configuration

1. Open the application code, create an **appsettings.Development.json** file in the **/source** folder. In the file, create a JSON object with **CosmosUri** and **CosmosKey** properties. Copy and paste the values for `URI` and `PRIMARY KEY` from the previous step:

    ```json
    {
      "CosmosUri": "<endpoint>",
      "CosmosKey": "<primary-key>"
    }
    ```

1. Open a terminal and run the application:

    ```bash
    dotnet run
    ```

1. Press Enter to observe the output as the program creates several objects based on property and array approaches for attributes. The application also outputs the results of various test queries.

### Query for Product Attributes

1. Navigate to the Azure portal (<https://portal.azure.com>), browse to your Azure Cosmos DB account. Go to the **Data Explorer**.

1. Select the **AttributeArrays** container, select **Items**. Scroll through each of the six documents in the container. The first three items show the data model when storing similar attributes as individual properties, the other three demonstrate how to model data using the attribute array pattern.

1. Let's explore the difference in querying for these two data models. In this example we are trying to return the product names and sizes where there are at least 75 items in stock in that size.

#### Query for attributes as properties

1. Select the **AttributeArrays** container. Select **New SQL Query**.

1. Paste the following to query for products when similar attributes are stored as individual properties.

    ```sql
    SELECT 
        p.name,
        p.sizeSmall,
        p.sizeMedium,
        p.sizeLarge
    FROM 
        products p
    WHERE
        p.sizeSmall >= 75 OR 
        p.sizeMedium >= 75 OR 
        p.sizeLarge >= 75
    ```

    Notice that due to the data model, you are forced to return sizes that do not meet the query criteria. This is something that has to be handled in the application. Also query construction is clumsy. As the type and number of attributes grows, this data model will grow more cumbersome and prone to bugs.

#### Query using attribute array pattern

1. Paste the following to query for products using attribute arrays:

    ```sql
    SELECT
        p.name,
        s.size,
        s.count
    FROM
        products p
    JOIN
        s IN p.sizes
    WHERE
        s.count >= 75
    ```

    Notice the remarkable difference in query construction. It is very clean. It also concisely returns the product name, size and count in stock for that size.

## Summary

By applying this pattern to similar properties in data model, you can reduce and simplify your indexing, simplify your queries, making them less prone to future bugs, and improve performance and cost.
