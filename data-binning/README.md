---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Data Binning
urlFragment: data-binning
description: Review this pattern on how to design and implement binning to optimize cost.
---

# Azure Cosmos DB design pattern: Data Binning

The binning pattern (sometimes called windowing pattern) is a design pattern used when data is generated at a high frequency and requires aggregate views of the data over specific intervals of time. For example, a device emits data every second, but users view it as an average over one minute intervals. 

There are two primary benefits from this pattern. First, it reduces the number of requests over the wire from the receiving endpoint to any backend storage. This saves compute costs and reduces network traffic. Second, it simultaneously simplifies and reduces compute cost on the database to produce aggregate views of data. In particular aggregate views with the shortest interval as these are calculated with the higest frequency. The impact for using this pattern is a function of the number of devices, the frequency of data and aggregates calculated.

> This sample demonstrates:
>
> - ✅ Design and implementation of data binning to optimize cost.
> - ✅ Simulation of sensor events and then binning them into 1 minute windows before storing them in Azure Cosmos DB.
>

## Common scenario

An example where binning is preferred is when working with sensor data from Internet of Things (IoT) devices. For example, a hotel chain has devices installed in all rooms to read the temperature and send events to a centralized service. Each of those devices is configured to send an event to Azure IoT Hub every 5 seconds. For a hotel chain with one thousand rooms across its locations, that results in 12,000 data points per minute that are captured. An online monitoring application and dashboard only needs to show results once per minute. By applying the binning pattern with a window of 1 minute, database writes are reduced from 12,000 to 1,000 inserts per minute with a single document that includes the device identifier, an array of all the data points collected, and any aggregates calculated over that period of time. This reduces the compute required for the write operations without losing any detail the application requires. 

Just as important, this also eliminates the need to run an expensive query every minute that aggregates all the datapoints across all 1000 devices. If every data point was inserted as it's own document every 5 seconds for all 1,000 devices, this query would need to first filter for every document over the last minute, then group by device and calculate any required aggregate values. If these aggregates are pre-calculated, this eliminates an expensive step in processing that is executed with the greatest frequency.

This potentially could be optimized even further. Rather than running a query every minute to get the latest document for each of the 1000 devices, you could potentially take all of the pre-calculated aggregates across all 1,000 devices and insert them as an array in a single document. The dashboard could then be powered by executing a point-read on one document every minute versus a query. The dashboard could also be powered by implementing the materialized view pattern from a second container. This is a pattern we cover in another design pattern. No matter what you do however, it is important to iterate, test and measure to find the design that works for you.

## Sample implementation

In this section we will walk through a case study on how to design and implement binning to optimize cost.

The demo code will simulate events and bucket them within the same console application. It accepts parameters for up to 25 devices for up to 10 minutes to simulate data. Events are generated every 5 seconds. The console app collects, aggregates, then writes these to Cosmos DB once a minute.

A sample of incoming events sent every 5 seconds would look like this (but this is not written to Azure Cosmos DB):

```json
{
  "deviceId": 1,
  "eventTimestamp": "12/30/2022 10:53:05 PM",
  "temperature": 71.3,
  "unit": "Fahrenheit",
  "receivedTimestamp": "12/30/2022 10:53:05.128 PM"
},
{
  "deviceId": 1,
  "eventTimestamp": "12/30/2022 10:53:10 PM",
  "temperature": 71.2,
  "unit": "Fahrenheit",
  "receivedTimestamp": "12/30/2022 10:53:10.101 PM"
},
{
  "deviceId": 1,
  "eventTimestamp": "12/30/2022 10:53:15 PM",
  "temperature": 71.1,
  "unit": "Fahrenheit",
  "receivedTimestamp": "12/30/2022 10:53:15.121 PM"
}
```

Once binning is applied to summarize to a 1 minute window, the resulting event would look like this and be writtend to Azure Cosmos DB:
```json
{
  "deviceId": 1,
  "eventTimestamp": "12/30/2022 10:53:00 PM",
  "avgTemperature": 71.2,
  "minTemperature": 71.1,
  "maxTemperature": 71.3,
  "numberOfReadings": 12,
  "readings": [
        {
            "eventTimestamp": "12/30/2022 10:53:05 PM",
            "temperature": 71.1
        },
        {
            "eventTimestamp": "12/30/2022 10:53:10 PM",
            "temperature": 71.1
        },
        {
            "eventTimestamp": "12/30/2022 10:53:15 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:20 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:25 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:30 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:35 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:40 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:45 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:50 PM",
            "temperature": 71.2
        },
        {
            "eventTimestamp": "12/30/2022 10:53:55 PM",
            "temperature": 71.3
        },
        {
            "eventTimestamp": "12/30/2022 10:54:00 PM",
            "temperature": 71.3
        }],
  "receivedTimestamp": "12/30/2022 10:54:00 PM"
}

```

Note: In the demo application, aggregated events are collected based on system time. The `numberOfReadings` will likely be less than 12 on the earliest `eventTimestamp` because that is usually a partial minute (from whenever the application is started until the first time current timestamp has seconds value of `00`).

## Try this implementation

In order to run the demos, you will need:

- [.NET 6.0 Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)

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
- Type `git clone git clone https://github.com/Azure-Samples/cosmos-db-design-patterns.git` and press enter.
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

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fdata-binning%2Fdevcontainer.json)

## Create an Azure Cosmos DB for NoSQL account

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. In the Data Explorer, create a new database named **CosmosPatterns** with shared autoscale throughput:

    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Throughput** | `1000` (*Autoscale*) |

**Note:** We are using shared database throughput because it can scale down to 100 RU/s when not running. This is the most cost effient when running at very small scale.

1. Create a container **DataBinning** with the following values:


    | | Value |
    | --- | --- |
    | **Database name** | `CosmosPatterns` |
    | **Container name** | `DataBinning` |
    | **Partition key path** | `/DeviceId` |

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

## Run the demo

Open a new terminal and run the included Console App (Program.cs) which generates events saves them bucketed by device and minute:

```bash
dotnet run
```

While the Console App is running you may return to the terminal window where the function was started to see that it is writing batches to Cosmos DB.

## Querying the binned sensor data
Once you have run the demo which generates data, you can run queries directly against the event source container by using **Data Explorer** in the Azure Portal.

1. In Azure Portal, browse to you Cosmos DB resource.
1. Select **Data Explorer** in the left menu.
1. Select your container, then choose **New SQL Query**. 
![Screenshot of creating a SQL Query in Data Explorer within the Azure portal.](./images/data-explorer-create-new-query-bucketing.png)

Below is an example query for checking how many minutes the maxTemperature or minTemperature has been out of the expected range for each device.

```sql
SELECT
  c.DeviceId,
  COUNT(1) as MinutesWithAnomaly
FROM SensorEvents c
WHERE (c.minTemperature < 68 OR c.maxTeperature > 80) 
GROUP BY c.DeviceId
```

An example to check each reading in the array and count how many events were out of the expected range, you can use the `IN` keyword to access each array element in the query:
```sql
SELECT
  c.DeviceId,
  Count(1) as ReadingAnomalies
FROM SensorEvents c 
  JOIN t IN c.readings
  WHERE t.temperature < 68 OR t.temperature > 80
GROUP BY c.DeviceId
```

Note: The generated data will likely have many values considered anomalies. You can test out different filters to create your own rules.

## Summary

When modeling data for any application it's important to consider how the data will be used. NoSQL databases provide the ability to model data in a hierarchy within a JSON document. Applying the data binning pattern allows you to collect and aggregate data for how it is most often used. This provides efficency at multiple levels and simplifies design. This example we demonstrated provides a simple pattern for optimization. This pattern can be expanded upon and combined with other patterns to optimize even further or in different ways to give you the best performance and efficiency for your solutions. 
