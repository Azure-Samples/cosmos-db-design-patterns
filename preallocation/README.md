---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Preallocation Patern
urlFragment: preallocation
description: This is an example that shows how preallocation is used to optimize the performance and efficiency of database operations by allocating resources or space in advance, rather than dynamically as needed. 
---

# Preallocation Pattern

The **preallocation pattern** in NoSQL databases refers to a strategy of reserving or allocating resources such as storage space, throughput, and other capacity-related aspects in advance, rather than dynamically as needed. This pattern aims to optimize the performance, scalability, and predictability of NoSQL database systems by reducing the overhead of resource allocation during runtime and ensuring that sufficient resources are available to handle workload spikes and growth.

In the context of NoSQL databases, the preallocation pattern can manifest in several ways:

1. **Storage Space Preallocation**: NoSQL databases, especially those with flexible schemas like document stores, can benefit from preallocating storage space for documents or data structures. This can help avoid frequent resizing and reallocation of storage as data grows, leading to more consistent performance and reduced storage fragmentation.

2. **Throughput Preallocation**: Many NoSQL databases, like key-value stores or column-family databases, offer throughput provisioning to handle the number of requests or operations per second. Preallocating sufficient throughput capacity ensures that the database can handle anticipated peak workloads without throttling or degradation of performance.

3. **Index Preallocation**: Preallocating index structures or metadata required for querying can help improve query performance. This is particularly relevant in databases that support complex queries, where efficient indexing is crucial for rapid data retrieval.

4. **Partition Key and Sharding Preallocation**: In distributed NoSQL databases, like wide-column stores or distributed document databases, preallocating partition keys and determining sharding strategies in advance can prevent hotspots and ensure an even distribution of data across nodes or partitions.

5. **Resource Planning**: Beyond storage and throughput, preallocating other resources like memory, cache, and network bandwidth can contribute to optimal performance and responsiveness of NoSQL databases.

The rationale behind the preallocation pattern is to reduce the impact of dynamic resource allocation operations during runtime, which can introduce latency, contention, and unpredictability in performance. By provisioning resources upfront based on expected workloads and growth projections, developers can create more predictable and stable database systems that are capable of handling a variety of scenarios without unexpected bottlenecks.

This pattern involved pre-allocating an collection of values in full rather than adding them one at a time later or utilize complex logic to determine a result.

This sample demonstrates:

- ✅ A hotel reservation system using the preallocation pattern.

## Common scenario

This pattern tends to go well with collections of dates or representations of values in an array that should be pre-created with a related item.

A couple examples include:

- Hotel room reservations for a room instance
- Available seats in a theatre for a movie instance.

> **NOTE** Azure Cosmos DB has an item size limit of 2MB for an item.  Be sure any document that you model with the pre-allocation pattern stays under this limit, if it goes over this limit, you will need to consider breaking the data into seperate items.


The main components of the models used in the sample include:

- Hotels that contains Rooms
- Reservations that include a RoomId and HotelId
- Rooms can contain AvailableDates

### Scenario: Non-preallocation and preallocation design patterns for hotel room reservations.

## Non-preallocation

In the non-preallocation pattern, you will see that a hotel is created with 10 rooms.  These rooms have no reservations and the process of checking for reservations would be to query for any existing reservations and then subtracting out all the dates.

- An example hotel:

```json
{
    "id": "hotel_1",
    "EntityType": "hotel",
    "hotelId": "hotel_1",
    "Name": "Microsoft Hotels Inc",
    "City": "Redmond",
    "Address": "1 Microsoft Way",
    "Rating": 0,
    "AvailableRooms": 0,
    "Rooms": [],
    "Reservations": []
}
```

- An example room that does not utilize pre-allocation of available dates:

```json
{
    "id": "room_0",
    "EntityType": "room",
    "hotelId": "hotel_1",
    "Name": null,
    "Type": null,
    "Status": null,
    "NoBeds": 0,
    "SizeInSqFt": 0,
    "Price": 0,
    "Available": false,
    "Description": null,
    "MaximumGuests": 0,
    "Features": [],
    "RoomImages": [],
    "Reviews": []    
}
```

- An example reservation, where the room is a part of the reservation item:

```json
{
    "id": "reservation_room_0_20230213",
    "EntityType": "reservation",
    "LeaseId": null,
    "LeasedUntil": null,
    "IsPaid": false,
    "Status": null,
    "StartDate": "2023-02-13T00:00:00",
    "EndDate": "2023-02-14T00:00:00",
    "CheckIn": "0001-01-01T00:00:00",
    "CheckOut": "0001-01-01T00:00:00",
    "Customer": null,
    "hotelId": "hotel_1",
    "RoomId": "room_0",
    "Room": {
        "id": "room_0",
        "EntityType": "room",
        "LeaseId": null,
        "LeasedUntil": null,
        "hotelId": "hotel_1",
        "Name": null,
        "Type": null,
        "Status": null,
        "NoBeds": 0,
        "SizeInSqFt": 0,
        "Price": 0,
        "Available": false,
        "Description": null,
        "MaximumGuests": 0,
        "AvailableDates": [],
        "Features": [],
        "RoomImages": [],
        "Reviews": []
    }
}
```

## Preallocation

In the following example you will see the reservation dates for a room being pre-allocated in a collection with a simple `IsAvailable` property for each date.  This will then make the process of finding available dates a bit easier when it comes to sending queries to the database.

```csharp
DateTime start = DateTime.Parse(DateTime.Now.ToString("01/01/yyyy"));
DateTime end = DateTime.Parse(DateTime.Now.ToString("12/31/yyyy"));

//add all the days for the year which can be queried later.
foreach (Room r in h.Rooms)
{
    int count = 0;

    while (start.AddDays(count) < end)
    {
        r.AvailableDates.Add(new AvailableDate { Date = start.AddDays(count), IsAvailable = true });

        count++;
    }
}
```

- A pre-allocation room will now look similar to the following:

```json
{
    "id": "room_1",
    "EntityType": "room",
    "LeaseId": null,
    "LeasedUntil": null,
    "hotelId": "hotel_2",
    "Name": null,
    "Type": null,
    "Status": null,
    "NoBeds": 0,
    "SizeInSqFt": 0,
    "Price": 0,
    "Available": false,
    "Description": null,
    "MaximumGuests": 0,
    "AvailableDates": [
        {
            "Date": "2023-01-01T00:00:00",
            "IsAvailable": true
        },
        {
            "Date": "2023-01-02T00:00:00",
            "IsAvailable": true
        }...
    ],
    "Features": [],
    "RoomImages": [],
    "Reviews": []
}
```

## Queries

By pre-allocating the room's reservation days, you can easily run the following query to find available dates for a particular room or set of rooms:

```sql
select c.id, count(ad) from c join ad in c.AvailableDates where ad.Date > '2023-01-01T00:00:00' and ad.Date < '2023-01-05T00:00:00' and c.hotelId = 'hotel_2' group by c.id
```

By not choosing the pre-allocation pattern, the alternative way to find available rooms for a set of dates will be more complex.  For example, without pre-allocation, you would need to query all reservations for a room then build a collection of available dates by substracting the reservation dates.  You can see a subset of this logic available in the `FindAvailableRooms` method of the `Hotel` class.

## Try this implementation

To run this demo, you will need to have:

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

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview) with a [free Azure Cosmos DB account](https://learn.microsoft.com/azure/cosmos-db/try-free). (*This option doesn't require an Azure subscription, just a GitHub account.*)

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. Open the new account in the Azure portal and record the **URI** and **PRIMARY KEY** fields. These fields can be found in the **Keys** section of the account's page within the portal.

1. 


1. In the Data Explorer, create a new database and container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `HotelApp` |
    | **Partition key path** | `/Id` |
    | **Throughput** | `400` (*Manual*) |

1. Open the application code in a GitHub Codespace:

    [![Illustration of a button with the GitHub icon and the text "Open in GitHub Codespaces."](../media/open-github-codespace-button.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=613998360)

## Set up environment variables

1. Once the template deployment is complete, select **Go to resource group**.
1. Select the new Azure Cosmos DB for NoSQL account.
1. From the navigation, under **Settings**, select **Keys**. The values you need for the environment variables for the demo are here.

1. Create 2 environment variables to run the demos:

    - `COSMOS_ENDPOINT`: set to the `URI` value on the Azure Cosmos DB account Keys blade.
    - `COSMOS_KEY`: set to the Read-Write `PRIMARY KEY` for the Azure Cosmos DB for NoSQL account

1. Open a terminal in your GitHub Codespace and create your Bash variables with the following syntax:

    ```bash
    export COSMOS_ENDPOINT="YOUR_COSMOS_ENDPOINT"
    export COSMOS_KEY="YOUR_COSMOS_KEY"

## Run the demo

1. Review the `program.cs` file.
1. Notice the following code snippets

```csharp
Hotel h = Hotel.CreateHotel("1");
```

> NOTE: This will create a new hotel object with the id.

```csharp
List<Room> rooms = Hotel.CreateRooms(h);
```

> NOTE: This will create a set of rooms for a hotel, you can change the numbner of rooms (set to 10) to create by modifying the appropriate property in the class.

1. From Visual Studio Code, start the app by running the following:

  ```bash
  dotnet build
  dotnet run
  ```
1. From Visual Studio, press **F5** to start the application.

## Query the data

The code will create two hotels.  One that contains reservations that are used to determine open dates and another hotel that uses pre-allocation of dates.

1. In Azure Portal, browse to you Cosmos DB resource.
1. Select **Data Explorer** in the left menu.
1. Select your container, then choose **New SQL Query**.
1. Run the following query to see the rooms created, notice that the 'AvailableDates' collection is not used.

 ```sql
 select * from c where c.EntityType = 'room' and c.hotelId = 'hotel_1'
 ```

 ```sql
 select * from c where c.EntityType = 'reservation' and c.hotelId = 'hotel_1'
 ```

1. Run the following query to see the rooms created for hotel '2', review the rooms and the 'AvailableDates' property that has the pre-populated with a set of dates:

  ```sql
  select * from c where c.EntityType = 'room' and c.hotelId = 'hotel_2'
  ```

## Summary

Pre-allocation allows for a much simpler design for queries and logic versus other approaches however it can come at the cost of a larger document in storage and memory given the pre-allocation of the data.
