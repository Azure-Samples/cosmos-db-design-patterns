---
page_type: sample
languages:
- azdeveloper
- aspx-csharp
- csharp
- nosql
products:
- azure
- azure-cosmos-db
urlFragment: design-patterns
name: Azure Cosmos DB Design Pattern
description: A collection that showcases a variety of design patterns that can be employed to build robust and efficient applications using Azure Cosmos DB's NoSQL capabilities.
---

# Azure Cosmos DB Design Pattern Samples

![Azure Cosmos DB](/media/azure-cosmos-db-logo.jpg)

Welcome to the Azure Cosmos DB Design Pattern Samples repository! This collection showcases a variety of design patterns that can be employed to build robust and efficient applications using Azure Cosmos DB's NoSQL capabilities. Each pattern addresses specific scenarios and challenges, offering guidance and best practices for implementation.

<p style="font-size: 20px;"><strong>📝 Note:</strong> For this and other great samples, visit the <a href="https://azurecosmosdb.github.io/gallery/" target="_blank">Azure Cosmos DB Samples Gallery</a>.</p>



## Importance of Design Patterns in Application Development and Data Modeling

Design patterns play a crucial role in building robust applications and modeling data effectively. They offer structured solutions to common challenges, providing numerous benefits that contribute to the success of your projects.

### Key Benefits of Using Design Patterns

- **Efficiency and Best Practices**: Design patterns encapsulate proven solutions, saving you time and effort by leveraging established best practices.
- **Scalability and Performance**: Many patterns are optimized for scalability, ensuring your application can handle growth without compromising performance.
- **Consistency and Maintainability**: Patterns promote consistent architecture, making codebases easier to understand, maintain, and extend.
- **Reliability and Resilience**: Patterns address fault tolerance and error handling, resulting in applications that gracefully recover from failures.
- **Flexibility and Adaptability**: Patterns facilitate changes, enabling your application to evolve and adapt to new requirements seamlessly.
- **Reusability and Accelerated Development**: Patterns encourage reusable components, speeding up development and reducing the risk of bugs.
- **Effective Data Modeling**: In NoSQL databases like Azure Cosmos DB, choosing the right pattern ensures efficient data modeling for enhanced performance.
- **Documentation and Communication**: Patterns provide a shared vocabulary, aiding communication and collaboration among team members.
- **Adherence to Best Practices**: Design patterns ensure applications adhere to security, data integrity, and maintainability best practices.
- **Reduced Learning Curve**: Developers familiar with patterns quickly understand and contribute to projects, reducing onboarding time.


## Design Patterns Included

Explore the following design patterns to enhance your understanding of building applications with Azure Cosmos DB:

### [Attribute Array](/attribute-array/)

This pattern demonstrates how to use attribute arrays to efficiently store and query multiple attributes of an entity within a single document. Dive into the `attribute-array` folder for a comprehensive guide on how to get started. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/attribute-array).

### [Data Binning](/data-binning/)

Learn how to leverage data binning to organize and group data points into predefined bins for easy analysis and retrieval. Discover the `data-binning` folder for step-by-step instructions on implementation. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/data-binning).

### [Distributed Counter](/distributed-counter/)

Implement a distributed counter to efficiently maintain and update counts across multiple documents. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/distributed-counter).


### [Distributed Lock](/distributed-lock/)

Explore the `distributed-lock` pattern to learn how to implement distributed locks for managing concurrent access to resources in Azure Cosmos DB.Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/global-distributed-lock).

### [Document Versioning](/document-versioning/)

Discover how to manage document versioning effectively within Azure Cosmos DB. The `document-versioning` folder provides guidance on handling document changes over time. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/document-versioning).

### [Event Sourcing](/event-sourcing/)

Uncover the power of event sourcing for building applications that maintain a history of changes as a sequence of events. Explore the `event-sourcing` folder for in-depth instructions. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/event-sourcing).

### [Loop-Safe Change Feed](/loop-safe-change-feed/)

Learn how to enrich documents **in place** from the change feed — deriving a value and writing it back onto the same document — without creating an infinite loop, by hashing the source so the echo of your own write is skipped. See the `loop-safe-change-feed` folder for a Change Feed Processor implementation and an interactive web front end.

### [Materialized View](/materialized-view/)

Learn how to create and manage materialized views to efficiently retrieve precomputed data from Azure Cosmos DB. Refer to the `materialized-view` folder for implementation details. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/materialized-view).  

### [Preallocation](/preallocation/)

Explore the `preallocation` pattern to understand how to preallocate resources, such as document IDs, to optimize performance and resource utilization. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/preallocation).  

### [Schema Versioning](/schema-versioning/)

Dive into the `schema-versioning` folder to learn how to manage changes to your data model over time with the schema versioning pattern. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/schemaversioning).  

### [Vector Search](/vector-search/)

Search your data by **meaning** rather than keywords using Azure Cosmos DB's built-in **vector indexing** and the `VectorDistance()` function — the storage-and-retrieval foundation of semantic search and retrieval-augmented generation (RAG). The `vector-search` folder includes a console app and an interactive web front end that embeds text with a small local model (no API key).

## Getting Started

### Prerequisites
If running locally you will need to install some pre-requistes.

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- [Docker](https://www.docker.com/) — to run the Azure Cosmos DB Linux emulator locally

To confirm you have the required versions of the tools installed.

First, check the .NET runtime with this command. Make sure that .NET components with versions that start with 10.0 appear as part of the output:

```bash
dotnet --list-runtimes
```

Next, check the version of Azure Functions Core Tools with this command. You should have a version 4.*x* installed.:

```bash
func --version
```

If you do not have this version installed, you will need to uninstall the older version and follow [these instructions for installing Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools).


### Using the Terminal:
- Open the terminal on your computer.
- Navigate to the directory where you want to clone the repository.
- Type `git clone https://github.com/Azure-Samples/cosmos-db-design-patterns.git` and press enter.
- The repository will be cloned to your local machine.

### Using Visual Studio Code:
- Open Visual Studio Code.
- Click on the **Source Control** icon in the left sidebar.
- Click on the **Clone Repository** button at the top of the Source Control panel.
- Paste `https://github.com/Azure-Samples/cosmos-db-design-patterns.git` into the text field and press enter.
- Select a directory where you want to clone the repository.
- The repository will be cloned to your local machine.

### Setting up Azure Cosmos DB

You can run every sample entirely locally against the **Azure Cosmos DB Linux (vNext) emulator** — no Azure subscription or account required. This is the default, recommended way to explore the patterns. Each sample also includes an optional [Azure Developer CLI (`azd`)](https://aka.ms/azd) template if you'd rather run it in Azure.

#### Run locally with the emulator (default)

From the root of this repository, start the emulator with Docker:

```bash
docker compose up -d
```

This starts the [Azure Cosmos DB Linux (vNext) emulator](https://learn.microsoft.com/azure/cosmos-db/emulator-linux) with its gateway on `https://localhost:8081` and the Data Explorer on `http://localhost:1234`. Point the samples at it with the well-known emulator endpoint and key:

| Setting | Value |
| --- | --- |
| Endpoint | `https://localhost:8081` |
| Key | `C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==` |

The console and website samples accept the emulator's self-signed certificate automatically when the endpoint is `localhost`, so no certificate setup is needed. The Functions samples (`event-sourcing`, `materialized-view`) run their Cosmos binding in the Functions **host** process, so they require trusting the emulator certificate once — see those samples' README for the steps.

When you're done, stop the emulator with `docker compose down`.

#### (Optional) Run in Azure

Each sample includes an [Azure Developer CLI (`azd`)](https://aka.ms/azd) template that provisions a dedicated, **keyless** (Microsoft Entra ID / managed identity) set of Azure resources for that one sample — and, for the web and Functions samples, deploys the app. Look for the **"(Optional) Deploy and run in Azure with `azd`"** section in each sample's `README.md`.

The `azd` templates are intentionally minimal and inexpensive — this repository teaches Cosmos DB **design patterns**, not production or enterprise architecture — and each is fully removed with `azd down` when you're finished.

#### Configuration and authentication

Each sample reads its Azure Cosmos DB endpoint (and, optionally, an account key) from configuration. The **recommended way to supply these values for local development is environment variables** — they keep secrets out of the source tree and work well with the emulator, containers, and CI. Every sample still also reads `appsettings.development.json` (git-ignored) as a file-based fallback if you prefer it.

The console and data-generator samples bind these values at the root of configuration; the website samples bind them under a `CosmosDb` section (so their environment variables use the `__` separator). The Functions samples (`event-sourcing`, `materialized-view`) use `local.settings.json` / `CosmosDBConnection`.

| Sample type | Endpoint variable | Key variable (optional) |
| --- | --- | --- |
| Console / data-generator | `CosmosUri` | `CosmosKey` |
| Website (Razor/Blazor) | `CosmosDb__CosmosUri` | `CosmosDb__CosmosKey` |

> Some samples read additional root-level settings (for example `distributed-lock` and `distributed-counter` use `CosmosDatabase` and `CosmosContainer`). See each sample's README for its full list.

**Option 1: Keyless authentication via RBAC (Recommended)**

The samples use `DefaultAzureCredential` from `Azure.Identity`. When the key is left empty (or unset), the application automatically uses RBAC-based authentication. This works with:
- Azure managed identity (when hosted in Azure)
- Azure CLI credentials (`az login`) for local development

Assign the **Cosmos DB Built-in Data Contributor** role to your identity before running locally:

```bash
az cosmosdb sql role assignment create \
  --account-name <cosmos-account-name> \
  --resource-group <resource-group-name> \
  --role-definition-name "Cosmos DB Built-in Data Contributor" \
  --principal-id $(az ad signed-in-user show --query id -o tsv) \
  --scope "/"
```

Then set only the endpoint (console/data-generator example shown):

```powershell
# PowerShell
$env:CosmosUri = "<endpoint>"
```
```bash
# bash
export CosmosUri="<endpoint>"
```

**Option 2: Key-based authentication (local emulator fallback)**

When using the Azure Cosmos DB Emulator or when RBAC is not available, also set the key:

```powershell
# PowerShell
$env:CosmosUri = "<endpoint>"; $env:CosmosKey = "<primary-key>"
```
```bash
# bash
export CosmosUri="<endpoint>"; export CosmosKey="<primary-key>"
```

> **Security note:** Never commit your Cosmos DB primary key or connection string to source control. Prefer environment variables (or RBAC). The `.gitignore` already excludes `appsettings.development.json` and `local.settings.json`, which remain supported as a fallback.

Happy coding with Azure Cosmos DB and these powerful design patterns!

## Contributions

We welcome contributions to this repository! If you have additional design patterns, improvements, or fixes, feel free to submit a pull request. 

## License

This repository is licensed under the [MIT License](LICENSE). Feel free to use and share these design patterns as you see fit.
