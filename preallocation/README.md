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

The pre-allocation pattern involves creating an initial empty structure that will be populated later. This approach simplifies the design of queries and logic compared to alternative methods. It should be noted that pre-allocating data can result in larger storage and RU usage but queries execute faster and the overall operations involved also tend to be faster as the query itself returns the necessary data versus having to manually merge data from multiple queries.

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

In the non-preallocation pattern, you will see that a hotel is created with 10 rooms.  These rooms have no reservations and the process of checking for reservations would be to query for all the rooms in the hotel, then querying for any existing reservations, then merging both datasets and subtracting out any rooms that have reservations for the dates being searched for.

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

- An example reservation item, where the room is a part of the reservation item:

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

In the following example you will see the reservation dates for a room being pre-allocated in a collection with a simple `IsReserved` property for each date.  This will then make the process of finding available dates easier and faster.

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

As you may have multiple versions of the runtime installed, make sure that .NET components with versions that start with 8.0 appear as part of the output.

## Getting the code

### Using Terminal or VS Code

Directions installing pre-requisites to run locally and for cloning this repository using [Terminal or VS Code](../README.md?#getting-started)


### GitHub Codespaces

Open the application code in GitHub Codespaces:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fpreallocation%2Fdevcontainer.json)

## Set up application configuration files

You need to configure the application configuration file to run these demos.

1. Go to resource group.

1. Select the Serverless Azure Cosmos DB for NoSQL account that you created for this repository.

1. From the navigation, under **Settings**, select **Keys**. The values you need for the application settings for the demo are here.

While on the Keys blade, make note of the `URI` and `PRIMARy KEY`. You will need these for the sections below.

1. Open the project and add a new **appsettings.development.json** file with the following contents:

  ```json
  {
    "CosmosUri": "",
    "CosmosKey": "",
    "DatabaseName": "PreallocationDB",
    "WithPreallocation": "WithPreallocation",
    "WithoutPreallocation": "WithoutPreallocation"
  }
    ```

1. Replace the `CosmosURI` and `CosmosKey` with the values from the Keys blade in the Azure Portal.
1. Modify the **Copy to Output Directory** to **Copy Always** (For VS Code add the XML below to the csproj file)
1. Save the file.

  ```xml
    <ItemGroup>
      <Content Update="appsettings.development.json">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
      </Content>
    </ItemGroup>
  ```

## Run the demo

1. Open the application code.
1. From Visual Studio Code, start the app by running the following:

    ```bash
    dotnet build
    dotnet run
    ```

    or
    From Visual Studio, press **F5** to start the application.

1. The application will automatically create a database called `PreallocationDB` with two containers, `WithPreallocation` and `WithoutPreallocation`.
1. Select option `1` in the console application to load the hotel and room data. 
    1. In Azure Portal, browse to the Azure Cosmos DB account for this respository.
    1. Select **Data Explorer** in the left menu.
    1. Locate and open the `PreallocationDB`
    1. Review the data in both containers. Notice different structure for both containers. The 'Reservation' entity type documents are not used in the `WithPreallocation` container. Instead the reservation dates for a room are pre-allocated in an array in each room document.

1. Select option `2` to run the query with out Preallocation. Provide a date in DD-MM-YYYY format. Note the RU Charge and elapsed time.
1. Select option `3` to run the same query using Preallocation. Provide a date in DD-MM-YYYY format. Note the RU Charge and elapsed time.
1. Compare the code for both options. Notice that Preallocation allows for faster response times. However it often comes at a cost of higher RU charge due to the larger document sizes. 
1. In the `Hotel.cs` view the queries for each method. Note the simpler design for queries and application logic using Preallocation  versus when not using it.

## Summary

Pre-allocation allows for a much simpler design for queries and logic versus other approaches. It can often yield faster reponse times as well. However it can come at the cost of a larger document in storage and RU charge given the pre-allocation of the data.
