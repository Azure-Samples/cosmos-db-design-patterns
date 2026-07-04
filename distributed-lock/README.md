---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
name: |
  Azure Cosmos DB design pattern: Global Distributed Lock
urlFragment: distributed-lock
description: Review this example of using a global distributed lock to coordinate and synchronize access to shared resources in a distributed system.
---

# Azure Cosmos DB design pattern: Global Distributed Lock Pattern

Locks are a way of synchronizing access to a shared resource in a distributed system. By allowing a process to acquire a token that indicates ownership of the resource. Other processes must wait until the token is released before they can acquire it. This ensures that only one process can access the resource at a time. Fence tokens are useful in scenarios where multiple processes need to access a shared resource but cannot do so concurrently.

Distributed locks are superior to regular locks in distributed systems because they enable synchronization of access to shared resources across multiple processes and machines. Regular locks can only provide synchronization within a single process or machine, which limits their applicability in distributed systems. Distributed locks are designed to handle the challenges of distributed systems, such as network delays, failures, and partitions. They also provide higher availability and fault tolerance, allowing the system to continue functioning even if some of the nodes fail. Additionally, distributed locks can be more scalable than regular locks, as they can be designed to work across a large number of nodes.

This sample demonstrates:

- ✅Optimistic concurrency control (ETag updates)
- ✅TTL (ability to set an expiration date on a document)

## Common scenario

A common scenario for using a distributed global lock in the NoSQL design pattern is when you need to enforce mutual exclusion or coordination across multiple nodes or processes in a distributed system. Here are a few examples:

1. Critical Sections: In a distributed system, there may be certain critical sections of code or operations that need to be executed atomically by a single node at a time. A distributed global lock can be used to ensure that only one node or process can enter the critical section at any given time, preventing conflicts and ensuring data consistency.

1. Resource Synchronization: When multiple nodes or processes need to access and modify a shared resource simultaneously, a distributed lock can be used to coordinate their access. For example, if multiple nodes are updating the same document in a document-oriented NoSQL database, a distributed lock can ensure that only one node can modify the document at a time, avoiding conflicts and maintaining data integrity.

1. Concurrency Control: Distributed locks can be used for concurrency control in scenarios where multiple nodes or processes are performing parallel operations on shared data. By acquiring a lock on a specific resource or data entity, a node can ensure exclusive access to that resource, preventing concurrent modifications that might lead to inconsistent or incorrect results.

1. Distributed Transactions: In a distributed transactional system, where multiple operations across different nodes need to be performed atomically, distributed locks play a crucial role. They help coordinate the different phases of a distributed transaction, ensuring that conflicting operations do not occur during the transaction's execution.

By using a distributed global lock, you can coordinate and synchronize the actions of multiple nodes or processes, providing consistency and preventing conflicts in a distributed environment. However, it's important to note that implementing distributed locks correctly and efficiently can be complex, and the specific mechanisms and techniques used may vary depending on the NoSQL database being used.

## Sample implementation

The application creates a Lock based on the Name and Time to Live( TTL) provided by the user. The Lock is created in Azure Cosmos DB and  then can be tracked by multiple geographically distributed worker threads. In this sample  the application creates 3  threads  that continuously try to get  the lock.  The worker thread holds the locks for a random number of milliseconds and then releases it. If the lock is not released with the TTL value, the lock gets released automatically.
![Screenshot showing the Distributed Lock Application running](media/dlock.png)

The TTL feature is used to automatically get rid of a lease object rather than having clients do the work of checking a leasedUntil date.  This takes away one step, but you are still required to check to see if two clients tried to get a lease on the same object at the same time.  This is easily done in Azure Cosmos DB via the 'etag' property on the object.

## Getting the code

### Using Terminal or VS Code

Directions installing pre-requisites to run locally and for cloning this repository using [Terminal or VS Code](../README.md?#getting-started)


### GitHub Codespaces

Open the application code in GitHub Codespaces:

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fdistributed-lock%2Fdevcontainer.json)

## Set up application configuration files

You need to configure an application configuration file to run this app.

1. Go to your resource group and select the Serverless Azure Cosmos DB for NoSQL account that you created for this repository.

1. From the navigation, under **Settings**, select **Keys** and copy the **URI** value.

### Option 1: Keyless authentication via RBAC (Recommended)

Keyless authentication using `DefaultAzureCredential` is the recommended approach. It works automatically with managed identity (Azure-hosted) and with the Azure CLI locally.

1. Assign the **Cosmos DB Built-in Data Contributor** role to your identity:

    ```bash
    az cosmosdb sql role assignment create \
      --account-name <cosmos-account-name> \
      --resource-group <resource-group-name> \
      --role-definition-name "Cosmos DB Built-in Data Contributor" \
      --principal-id $(az ad signed-in-user show --query id -o tsv) \
      --scope "/"
    ```

1. Sign in with the Azure CLI (for local development):

    ```bash
    az login
    ```

1. Open the distributed-lock project and add a new **appsettings.development.json** file with the following contents:

```json
{
  "CosmosUri": "<endpoint>",
  "CosmosDatabase": "LockDB",
  "CosmosContainer": "Locks",
  "retryInterval": 1
}
```

1. Replace `<endpoint>` with the **URI** value copied from the Keys blade.

### Option 2: Key-based authentication (local emulator fallback)

If you are using the Azure Cosmos DB Emulator or cannot use RBAC, set `CosmosKey` as well:

1. From the Keys blade, copy both the **URI** and **PRIMARY KEY** values.

1. Open the distributed-lock project and add a new **appsettings.development.json** file with the following contents:

```json
{
  "CosmosUri": "<endpoint>",
  "CosmosKey": "<primary-key>",
  "CosmosDatabase": "LockDB",
  "CosmosContainer": "Locks",
  "retryInterval": 1
}
```

> **Note:** Never commit `appsettings.development.json` with real key values. The `.gitignore` already excludes `appsettings.development.json`.

1. Modify the **Copy to Output Directory** to **Copy Always** (For VS Code add the XML below to the csproj file)
1. Save the file.

  ```xml
    <ItemGroup>
      <Content Update="appsettings.development.json">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
      </Content>
    </ItemGroup>
  ```

## Run the demo locally

1. At a command prompt or VS Code Terminal, switch to the `source` folder and run the app with:

    ```dotnetcli
    dotnet run
    ```

1. When prompted, enter the values for the lock name and the default TTL

## Summary

Azure Cosmos DB makes implementing a global lock fairly simple by utilizing the `TTL` and 'ETag' features.
