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
- ✅TTL (ability to set an expiration date on a record)

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

There are a few ways you can start working with the code in this demo.

### **Clone the Repository to Your Local Computer:**

**Using the Terminal:**

- Open the terminal on your computer.
- Navigate to the directory where you want to clone the repository.
- Type `git clone https://github.com/AzureCosmosDB/design-patterns.git` and press enter.
- The repository will be cloned to your local machine.

**Using Visual Studio Code:**

- Open Visual Studio Code.
- Click on the **Source Control** icon in the left sidebar.
- Click on the **Clone Repository** button at the top of the Source Control panel.
- Paste `https://github.com/AzureCosmosDB/design-patterns.git` into the text field and press enter.
- Select a directory where you want to clone the repository.
- The repository will be cloned to your local machine.

### **Fork the Repository:**

Forking the repository allows you to create your own copy of the repository under your GitHub account. This copy is independent of the original repository and is stored on your account. You can make changes to your forked copy without affecting the original repository. To fork the repository:

- Visit the repository URL: [https://github.com/AzureCosmosDB/design-patterns](https://github.com/AzureCosmosDB/design-patterns)
- Click the "Fork" button at the top right corner of the repository page.
- Select where you want to fork the repository (your personal account or an organization).
- After forking, you'll have your own copy of the repository under your account. You can make changes, create branches, and push your changes back to your fork.
- After forking the repository, open the repository on GitHub: [https://github.com/YourUsername/design-patterns](https://github.com/YourUsername/design-patterns) (replace `YourUsername` with your GitHub username).
- Click the "Code" button and copy the URL (HTTPS or SSH) of the repository.
- Open a terminal on your local computer and navigate to the directory where you want to clone the repository using the `cd` command.
- Run the command: `git clone <repository_url>` (replace `<repository_url>` with the copied URL).
- This will create a local copy of the repository on your computer, which you can modify and work with.

### **GitHub Codespaces**

You can try out this implementation by running the code in [GitHub Codespaces](https://docs.github.com/codespaces/overview) with a [free Azure Cosmos DB account](https://learn.microsoft.com/azure/cosmos-db/try-free). (*This option doesn't require an Azure subscription, just a GitHub account.*)

- Open the application code in a GitHub Codespace:

    [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/azure-samples/cosmos-db-design-patterns?quickstart=1&devcontainer_path=.devcontainer%2Fschema-versioning%2Fdevcontainer.json)

## Create an Azure Cosmos DB for NoSQL account

1. Create a free Azure Cosmos DB for NoSQL account: (<https://cosmos.azure.com/try>)

1. In the Data Explorer, create a new databased named **LockDB** with a small amount of throughput assigned:

    | | Value |
    | --- | --- |
    | **Database name** | `LockDB` |
    | **Throughput** | `400` (*Manual*) |

1. Create a container named **Locks** container with the following values:

    | | Value |
    | --- | --- |
    | **Database name** | `LockDB` |
    | **Container name** | `Locks` |
    | **Partition key path** | `/id` |

1. From the navigation, under **Settings**, select **Keys**. The values you need for the environment variables for the demo are here.

## Configure the application

1. Open the code, create an **appsettings.Development.json** file in the **/consoleapp** folders. In this files, create a JSON object with **CosmosUri** and **CosmosKey** properties. Use the URI and primary key you recorded earlier for these values:

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",

  "CosmosUri": "",
  "CosmosKey": "",
  "CosmosDatabase": "LockDB",
  "CosmosContainer": "Locks",
  "retryInterval": 1

}
```

1. In the codespace, open a terminal and run the application:

```bash
dotnet run
```

## Summary

Azure Cosmos DB makes implementing global lock leases a fairly simple by utilizing the `TTL` and 'ETag' features.
