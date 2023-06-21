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

The attribute array pattern creates JSON arrays consisting of multiple similar properties or fields. These fields should be grouped together in a collection for better indexing and sorting.

The main advantage to this pattern is that rather creating multiple indexes for every property/field, we can now focus on one particular path to index. In the case where you have to add another property/field later, it can be easily added to the collection. You can add this attribute easily versus the typical data model change and reindex procedure necessary for property-based attributes.

This sample demonstrates:

- ✅ Creation of several objects based on property and array attribute data patterns.
- ✅ Querying both property and array objects in a container.

## Common scenario

A common scenario for using a NoSQL attribute array design pattern is when you have entities with a large number of attributes that can vary in number and type. This pattern is useful when the attributes of an entity aren't well-defined or fixed, and you want to avoid schema changes or migrations.

Let's say you're developing an e-commerce platform where sellers can list their products. Each product can have various attributes such as size, color, brand, price, description, and more. However, different sellers may have different attributes for their products. Some sellers might want to include more attributes like weight, material, or warranty.

In a relational database, you would typically create a table with predefined columns for each attribute. However, in this case, the number and type of attributes can vary greatly between different products and sellers, making it difficult to define a fixed schema.

Using a NoSQL database with an attribute array design pattern, you can store the product attributes as a flexible array or document within the product entity. Each attribute can be represented as a key-value pair, where the key is the attribute name (for example, "color") and the value is the corresponding attribute value (for example, "red").

With this design pattern, you can easily accommodate the varying attributes of different products and sellers. Sellers can add or remove attributes as needed without requiring schema modifications or complex migrations. Queries can be performed on specific attribute values, and you can even index certain attributes for efficient searching.

Overall, the NoSQL attribute array design pattern is suitable when you have entities with dynamic and variable attributes. This pattern allows for flexibility, scalability, and easy adaptability to changing requirements.

## Sample implementation

Here are two scenarios where you may want to create an attribute array to capture a variable number of attributes for each item.

### Products with sizes

Products like shirts and sweaters tend to have multiple sizes that may be in inventory. Based on the size, you could design your model to look like the following with each size count as a property/field:

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
  "price": 895.3737123141316,
  "sizeSmall": 24,
  "sizeMedium": 61,
  "sizeLarge": 51
}
```

If you wished to find if any of the sizes (small, medium, or large) had a quantity of greater than 50, you would need to use multiple `OR` expressions like in this query:

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
    string Name,
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
  "price": 480.7020008526409,
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

This pattern results in a simpler, more flexible, and future-proof query using a `JOIN` expression:

```sql
SELECT VALUE p FROM products p JOIN s IN p.sizes WHERE s.count >= 75
```

You won't need to rewrite this query if you decide to add new sizes to your dataset in the future.

### Rooms with different prices

Another example could utilize hotel rooms with different prices based on currency or measurements based on units.  In the following example, the `PriceUSD` and `PriceEUR` are properties that hold pricing information based on their respective currencies. The `SizeSquareMeters` and `SizeSquareFeet` properties include measurements for the room based on different units.

```csharp
record Room(
    string Id,
    string HotelId,
    string LeaseId,
    DateTime LeasedUntil,
    int MaxGuests,
    decimal PriceUSD,
    decimal PriceEUR,
    int SizeSquareMeters,
    int SizeSquareFeet
);
```

The object JSON saved to Azure Cosmos DB would look like the following example. Notice the attributes for each room's price per currency and attributes for measurements per unit:

```json
{
  "id": "40c682bb-f0d3-dcbd-c607-d5b908831524",
  "hotelId": "acaf7963-53fc-af44-084b-d00304334bd2",
  "leaseId": "fb5a31a5-8e3b-350b-31f1-ae3cf2468511",
  "leasedUntil": "2023-06-24T09:21:10.3810905+00:00",
  "maxGuests": 4,
  "priceUSD": 307.5939613627272,
  "priceEUR": 576.707903611712,
  "sizeSquareMeters": 232,
  "sizeSquareFeet": 194
}
```

SQL queries would be required to "predict" which properties could exist on the items:

```sql
SELECT VALUE r FROM rooms r WHERE r.priceEUR >= 750 OR r.priceUSD >= 750
```

```sql
SELECT VALUE r FROM rooms r WHERE r.sizeSquareMeters >= 200 OR r.sizeSquareFeet >= 200
```

An attribute array alternative would be to create a collection of prices and separate collection of sizes.

```csharp
record Room(
    string Id,
    string HotelId,
    string LeaseId,
    DateTime LeasedUntil,
    int MaxGuests,
    IList<RoomSize> Sizes,
    IList<RoomPrice> Prices
);

record RoomSize(
    string UnitMeasurement,
    int Size
);

record RoomPrice(
    string Currency,
    decimal Price
);
```

The object JSON saved to Azure Cosmos DB would look like the following. Notice how the prices are now part of a collection called `prices` and sizes are in their own collection named `sizes`:

```json
{
  "id": "c51eca32-ffef-5d95-db73-0ffd5c236c32",
  "hotelId": "acaf7963-53fc-af44-084b-d00304334bd2",
  "leaseId": "fdbd5516-c326-8f6f-db46-a5a5eaa0bd16",
  "leasedUntil": "2023-09-28T09:53:50.5959552+00:00",
  "maxGuests": 4,
  "sizes": [
    {
      "unitMeasurement": "SquareMeters",
      "size": 233
    },
    {
      "unitMeasurement": "SquareFeet",
      "size": 134
    }
  ],
  "prices": [
    {
      "currency": "USD",
      "price": 837.3416458154072
    },
    {
      "currency": "EUR",
      "price": 207.43372504784884
    }
  ]
}
```

This pattern results in queries that are far simpler to parse and future-proof against the addition of new attributes:

```sql
SELECT VALUE r FROM room r JOIN p IN r.prices WHERE p.price >= 750
```

```sql
SELECT VALUE r FROM room r JOIN s IN r.sizes WHERE s.size >= 200
```

## Try this implementation

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview) with a [free Azure Cosmos DB account](https://learn.microsoft.com/azure/cosmos-db/try-free). (*This option doesn't require an Azure subscription, just a GitHub account.*)

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. Open the new account in the Azure portal (<https://portal.azure.com>) and record the **URI** and **PRIMARY KEY** fields. These fields can be found in the **Keys** section of the account's page within the portal.

1. In the Data Explorer, create a new databased named **CosmosPatterns** with a small amount of throughput assigned:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Throughput** | `400` (*Manual*) |
    
1. Create a new **CosmosPatterns** database and a **Hotels** container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `Hotels` |
    | **Partition key path** | `/hotelId` |

1. Create another new container using the **CosmosPatters** database for the **Products** container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `Products` |
    | **Partition key path** | `/productId` |

1. Open the application code in a GitHub Codespace:

    [![Illustration of a button with the GitHub icon and the text "Open in GitHub Codespaces."](../media/open-github-codespace-button.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=613998360&devcontainer_path=.devcontainer%2Fattribute-array%2Fdevcontainer.json)

1. In the codespace, create an **appsettings.Development.json** file in both the **/visualizer** and **/consumerapp** folders. In each of the files, create a JSON object with **CosmosUri** and **CosmosKey** properties. Use the URI and primary key you recorded earlier for these values:

    ```json
    {
      "CosmosUri": "<endpoint>",
      "CosmosKey": "<primary-key>"
    }
    ```

1. In the codespace, open a terminal and run the application:

    ```bash
    dotnet run
    ```

1. Once complete, the progam creates several objects based on property and array approaches for attributes. The application outputs the results of various test queries.

1. Navigate to the Azure portal (<https://portal.azure.com>) again, and browse to your Azure Cosmos DB account. Go back to the **Data Explorer**.

1. Select the **Products** container, and then select **New SQL Query**.

1. Run the following test queries for the **Products** container.

    - Query for products using attribute properties:

        ```sql
        SELECT VALUE p FROM products p WHERE p.sizeSmall >= 75 OR p.sizeMedium >= 75 OR p.sizeLarge >= 75
        ```

    - Query for products using attribute arrays:

        ```sql
        SELECT VALUE p FROM products p JOIN s IN p.sizes WHERE s.count >= 75
        ```

1. Now, select the **Hotels** container, and then select **New SQL Query**.

1. Run the following test queries for the **Hotels** container.

    - Query for hotels

        ```sql
        SELECT VALUE r FROM room r WHERE r.entityType = 'Hotel'
        ```

    - Query for hotel rooms using attribute properties:

        ```sql
        SELECT VALUE r FROM rooms r WHERE r.priceEUR >= 750 OR r.priceUSD >= 750
        ```

        ```sql
        SELECT VALUE r FROM rooms r WHERE r.sizeSquareMeters >= 200 OR r.sizeSquareFeet >= 200
        ```

    - Query for hotel rooms using attribute arrays:

        ```sql
        SELECT VALUE r FROM room r JOIN p IN r.prices WHERE p.price >= 750
        ```

        ```sql
        SELECT VALUE r FROM room r JOIN s IN r.sizes WHERE s.size >= 200
        ```

## Summary

By converting similar properties\fields to collections, you can improve many aspects of your data model and the queries that run against them.  You can also reduce and simplify the indexing settings on a container and make queries easier to write and also execute.
