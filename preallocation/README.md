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

The pre-allocation pattern involves creating an initial empty structure that will be populated later. This approach simplifies the design of queries and logic compared to alternative methods. However, it should be noted that pre-allocating data can result in larger storage and RU usage.

This sample demonstrates:

- ✅ A hotel reservation system using the preallocation pattern.

## Common scenario

This pattern tends to go well with collections of dates or representations of values in an array that should be pre-created with a related item.

A couple examples include:

- Hotel room reservations for a room instance
- Available seats in a theatre for a movie instance.

> **NOTE** Azure Cosmos DB has an item size limit of 2MB for an item.  Be sure any document that you model with the pre-allocation pattern stays under this limit, if it goes over this limit, you will need to consider breaking the data into separate items.

The main components of the models used in the sample include:

- Hotels that contains Rooms
- Reservations that include a RoomId and HotelId
- Rooms can contain ReservationDates

## Scenario: Non-preallocation and preallocation design patterns for hotel room reservations

### Non-preallocation

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
    "ReservationDates": [],
    "Features": [],
    "RoomImages": [],
    "Reviews": []
  }
}
```

### Preallocation

In the following example you will see the reservation dates for a room being pre-allocated in a collection with a simple `IsReserved` property for each date.  This will then make the process of finding available dates a bit easier when it comes to sending queries to the database.

```csharp
DateTime start = DateTime.Parse(DateTime.Now.ToString("01/01/yyyy"));
DateTime end = DateTime.Parse(DateTime.Now.ToString("12/31/yyyy"));

//add all the days for the year which can be queried later.
foreach (Room r in h.Rooms)
{
    int count = 0;

    while (start.AddDays(count) < end)
    {
        r.ReservationDates.Add(new ReservationDate { Date = start.AddDays(count), IsReserved = true });

        count++;
    }
}
```

- A pre-allocation room will now look similar to the following:

```json
{
  "id": "room_0",
  "_etag": "\"de016f0a-0000-0700-0000-64f19c080000\"",
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
  "ReservationDates": [
    {
      "Date": "2023-09-01T00:00:00Z",
      "IsReserved": true
    },
    {
      "Date": "2023-09-02T00:00:00Z",
      "IsReserved": false
    },
    {
      "Date": "2023-09-03T00:00:00Z",
      "IsReserved": false
    },...
  ]
}
```

## Queries

By pre-allocating the room's reservation days, you can easily run the following query to find available dates for a particular room or set of rooms:

```sql
SELECT
    a.Date,
    a.IsReserved,
    r.hotelId
FROM room r
    JOIN a IN r.ReservationDates
WHERE
    a.Date >= '2023-09-01T00:00:00Z' AND
    a.Date < '2023-09-02T00:00:00Z' AND
    a.IsReserved = false AND
    r.hotelId = 'hotel_1'
```

By not choosing the pre-allocation pattern, the alternative way to find available rooms for a set of dates will be more complex.  For example, without pre-allocation, you would need to query all reservations for a room then build a collection of available dates by subtracting the reservation dates.  You can see a subset of this logic available in the `FindAvailableRooms` method of the `Hotel` class.

## Try this implementation

To run this demo, you will need to have:

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download)

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

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fpreallocation%2Fdevcontainer.json)

## Create an Azure Cosmos DB for NoSQL account

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. In the Data Explorer, create a new databased named **CosmosPatterns** with shared autoscale throughput and a container **WithPreallocation**:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `WithPreallocation` |
    | **Partition key path** | `/id` |
    | **Throughput** | `1000` (*Autoscale*) |

1. Create a second container in the same `CosmosPatterns` database named `WithoutPreallocation`:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `WithoutPreallocation` |
    | **Partition key path** | `/id` |

**Note:** We are using shared database throughput because it can scale down to 100 RU/s when not running. This is the most cost efficient if running in a paid subscription and not using Free Tier.

## Updating Azure Cosmos DB URI and Key in Code

1. Once the account deployment is complete, select the new Azure Cosmos DB for NoSQL account.

1. Open the Keys blade, click the Eye icon to view the `PRIMARY KEY`. Keep this and the `URI` handy. You will need these for the next step.
Update the following in the **appsettings.json**  before you run the code:

- `CosmosUri`: Set to the `URI` value on the Azure Cosmos DB account Keys blade.
- `CosmosKey`: Set to the Read-Write `PRIMARY KEY` for the Azure Cosmos DB for NoSQL account

## Run the demo

1. Open the application code.
2. From Visual Studio Code, start the app by running the following:

    ```bash
    dotnet build
    dotnet run
    ```

    or
    From Visual Studio, press **F5** to start the application.

3. Select option `1` in the console application to build the sample database. This option will create a database called `CosmosPatterns` which will have 2 containers called `HotelApp_containerWithPreallocation` and `HotelApp_containerWithoutPreallocation`.
    1. In Azure Portal, browse to you Cosmos DB resource.
    2. Select **Data Explorer** in the left menu.
    3. Review the data in both  container, notice that the 'Reservation' documents is not used in the *HotelApp_containerWithPreallocation*, instead the reservation dates for a room are pre-allocated in a collection.

4. Select option `2` to run the query with out any Preallocation. Provide a date in DD-MM-YYYY format.
5. Select option `3` to run the same query using Preallocation. Provide a date in DD-MM-YYYY format.
6. Compare the code for both Step# 4 and Step# 5. Notice that Pre-allocation allows for a much simpler design for queries and logic versus other approaches however it can come at the cost of a larger document in storage and memory given the pre-allocation of the data.

## Summary

Pre-allocation allows for a much simpler design for queries and logic versus other approaches however it can come at the cost of a larger document in storage and RU charge given the pre-allocation of the data.
