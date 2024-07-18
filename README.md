# Azure Cosmos DB Design Pattern Samples

![Azure Cosmos DB](/media/azure-cosmos-db-logo.jpg)

Welcome to the Azure Cosmos DB Design Pattern Samples repository! This collection showcases a variety of design patterns that can be employed to build robust and efficient applications using Azure Cosmos DB's NoSQL capabilities. Each pattern addresses specific scenarios and challenges, offering guidance and best practices for implementation.

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

### [Materialized View](/materialized-view/)

Learn how to create and manage materialized views to efficiently retrieve precomputed data from Azure Cosmos DB. Refer to the `materialized-view` folder for implementation details. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/materialized-view).  

### [Preallocation](/preallocation/)

Explore the `preallocation` pattern to understand how to preallocate resources, such as document IDs, to optimize performance and resource utilization. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/preallocation).  

### [Schema Versioning](/schema-versioning/)

Dive into the `schema-versioning` folder to learn how to manage changes to your data model over time with the schema versioning pattern. Read more about this design pattern in this [blog post](https://aka.ms/cosmosdbdesignpatterns/schemaversioning).  

## Getting Started

### Prerequisites
If running locally you will need to install some pre-requistes.

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)

To confirm you have the required versions of the tools installed.

First, check the .NET runtime with this command. Make sure that .NET components with versions that start with 8.0 appear as part of the output:

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

### Using GitHub Codespaces

Nearly all of these samples are configured to run from [GitHub Codespaces](https://docs.github.com/codespaces/overview).

Navigate to the individual folders of each design pattern for a dedicated `README.md` file and look for the GitHub Codespaced badge.

### Setting up Azure Cosmos DB

All of these design patterns are built to run from a single Serverless Azure Cosmos DB account. Before running any of the samples, click the Deploy to Azure button below to create a Serverless Azure Cosmos DB account. You will need the URI Primary Key and Connection String for these. Keep those handy as you prepare each sample to run.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fgithub.com%2FAzureCosmosDB%2Fdesign-patterns%2Ftree%2Fmain%2Fazuredeploy.json)

Happy coding with Azure Cosmos DB and these powerful design patterns!

## Contributions

We welcome contributions to this repository! If you have additional design patterns, improvements, or fixes, feel free to submit a pull request. 

## License

This repository is licensed under the [MIT License](LICENSE). Feel free to use and share these design patterns as you see fit.
