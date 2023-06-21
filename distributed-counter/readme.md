---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Distributed counter
urlFragment: distributed-counter
description: Review this example of the distributed counter pattern to keep track of a number in a high concurrency environment.
---

# Azure Cosmos DB design pattern: Distributed counter

In a high concurrency application, many client applications may need to update a counter property within a single item in real-time. Typically, this update operation would cause a concurrency issues or contention. The distributed counter pattern solves this problem by managing the increment/decrement of a counter separately from the impacted item.

This sample demonstrates:

- ✅ Creation of multiple distributed counters using the value of a primary counter.
- ✅ On-demand splitting and merging of distributed counters.
- ✅ Calculation of an aggregated value from the distributed counters at any given time.
- ✅ Modifying the distributed counters randomly using a large number of worker threads.

## Common scenario

Consider a high-traffic website that tracks the inventory of a product in real-time for customers and internal services. In this high concurrency environment, updating a single document continuously causes significant contention.

Typically, you can avoid concurrency issues by implementing optimistic concurrency control, but this strategy may cause scenarios where the latest inventory isn't accurate in real-time. It's important for all aspects of the application to be able to update the count quickly and read a highly accurate count value in real-time.

## Solution

In a distributed counter solution, a group of distributed counter items are used to keep track of the number. By having the solution distribute the counter across multiple items, update operations can be performed on a random item without causing contention. Even more, the solution can calculate the total of all counters at any time using an aggregation of the values from each individual counter.

## Sample implementation

This sample is implemented as a C#/.NET application with three projects. The three projects are described here:

- **Counter** class library:

  - This class library implements the distributed counter pattern using two services.

  - The `DistributedCounterManagementService` creates the counter and manages on-demand splitting or merging.

  - The `DistributedCounterOperationalService` updates the counters in a high traffic workload. This service picks a random distributed counter from the pool of available counters and updates the counter using a partial document update (or HTTP PATCH request). This service's implementation ensures that there are no conflicts to updating the counter and each counter update is an atomic transaction.

- **Visualizer** web application:

  - This Blazor web application renders a visual interface for the distributed counters.

  - The web application uses graphical charts to illustrate how the counters are performing in real-time.
  
  - The web application polls the `DistributedCounterManagementService` for the data rendered in the chart.

  ![Screenshot of the Blazor web application with a chart visualizing the various distributed counters.](media/distributed-counter-chart-visualization.png)

- **Consumers** console application:

  - This console application mimics a high traffic workload.

  - The console application creates multiple concurrent threads. Each thread runs in a loop to update the distributed counters quickly.

  - The console application uses the `DistributedCounterOperationalService` to update the counters.

  ```output
  ── Starting distributed counter consumer... ───────────────────────────────
  What is the counter ID? ecfd48fc-002e-49cc-a355-40eaf1ea69c3
  What are the number of worker threads required? 3
  ── 3 worker threads are running... ────────────────────────────────────────
  Success         Decrement by 2
  ...
  ```

## Try this implementation

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview) with a [free Azure Cosmos DB account](https://learn.microsoft.com/azure/cosmos-db/try-free). (*This option doesn't require an Azure subscription, just a GitHub account.*)

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. Open the new account in the Azure portal and record the **URI** and **PRIMARY KEY** fields. These fields can be found in the **Keys** section of the account's page within the portal.

1. In the Data Explorer, create a new database and container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `CounterDB` |
    | **Container name** | `Counters` |
    | **Partition key path** | `/pk` |
    | **Throughput** | `400` (*Manual*) |

1. Open the application code in a GitHub Codespace:

    [![Illustration of a button with the GitHub icon and the text "Open in GitHub Codespaces."](../media/open-github-codespace-button.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=613998360&devcontainer_path=.devcontainer%2Fdistributed-counter%2Fdevcontainer.json)

1. In the codespace, create an **appsettings.Development.json** file in both the **/visualizer** and **/consumerapp** folders. In each of the files, create a JSON object with **CosmosUri** and **CosmosKey** properties. Use the URI and primary key you recorded earlier for these values:

    ```json
    {
      "CosmosUri": "<endpoint>",
      "CosmosKey": "<primary-key>"
    }
    ```

1. In the codespace, open a terminal and run the web application. The web application opens in a new browser window.

    ```bash
    cd Visualizer
    dotnet run
    ```

1. In the web application, create a new counter using the default settings.

    ![Screenshot of the new distributed counter configuration settings.](media/distributed-counter-configuration-settings.png)

1. Record the value of the **Counter ID** field in the web application.

    ![Screenshot of the distributed counter starting page with the identifier rendered.](media/distributed-counter-identifier.png)

1. Back in the codespace, open a second terminal and run the console application. The console application prompts you for the counter's unique identifier and a count of worker threads to use.

    ```bash
    cd ConsumerApp
    dotnet run
    ```

    ```output
    ── Starting distributed counter consumer... ───────────────────────────────
    What is the counter ID? ecfd48fc-002e-49cc-a355-40eaf1ea69c3
    What are the number of worker threads required? 3
    ── 3 worker threads are running... ────────────────────────────────────────
    Success         Decrement by 2
    Success         Decrement by 3
    Success         Decrement by 3
    Success         Decrement by 1
    Success         Decrement by 1
    Success         Decrement by 2
    Success         Decrement by 3
    Success         Decrement by 2
    Success         Decrement by 1
    Success         Decrement by 1
    Success         Decrement by 3
    ...
    Failed          Attemped to decrement by 2
    Failed          Attemped to decrement by 3
    Failed          Attemped to decrement by 2
    ```

1. Go back to the web application and observe the counters values change over time.

    ![Screenshot of the dynamic graph updated with distributed counter values.](media/distributed-counter-graph.png)

## Next steps

Learn more about the features of Azure Cosmos DB showcased in this example.

- [Partial document update](https://learn.microsoft.com/azure/cosmos-db/partial-document-update)
