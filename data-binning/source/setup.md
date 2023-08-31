# Bucketing Pattern Demo

This folder contains an Azure Function that will simulate sensor events and bucket them into 1 minute windows before saving to Cosmos DB. It also includes Program.cs that will trigger the function with default values of 10 devices for 3 minutes.

## CosmosPatternsBucketingExample function

To run the function app for Bucketing pattern, you will need to have:

- [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- 2 environment variables:
  - `COSMOS_ENDPOINT`: set to the Azure Cosmos DB for NoSQL endpoint
  - `COSMOS_KEY`: set to the Read-Write key for the Azure Cosmos DB for NoSQL account
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- Add a file to the folder `Cosmos_Patterns_Bucketing` called **local.settings.json** with the following contents:

    ```json
    {
        "IsEncrypted": false,
        "Values": {
            "AzureWebJobsStorage": "UseDevelopmentStorage=true",
            "FUNCTIONS_WORKER_RUNTIME": "dotnet"
        }
    }
    ```

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

This template will create an Azure Cosmos DB for NoSQL account with a database named `Hotels` with a container named `SensorEvents`. The partition key is set for `/DeviceId`. The data generator defaults to these values.

The suggested account name includes 'YOUR_SUFFIX'. Change that to a suffix to make your account name unique.

The Azure Cosmos DB for NoSQL account will automatically be created with the region of the selected resource group.

There is an option to enable the free tier. This is so that others can try this out with minimal costs to them.

**This link will work if this is a public repo.**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsolliancenet%2Fcosmos-db-nosql-modeling%2Fmain%2Fbucketing%2Fcode%2Fazuredeploy.json)

**For the private repo**

1. [Create a custom template deployment](https://portal.azure.com/#create/Microsoft.Template).
2. Select **Build your own template in the editor**.
3. Copy the contents from [this template](azuredeploy.json) into the editor.
4. Select **Save**.

---

Once the template is loaded, populate the values:

- **Subscription** - Choose a subscription.
- **Resource group** - Choose a resource group.
- **Region** - Select a region for the instance.
- **Location** - Enter a location for the Azure Cosmos DB for NoSQL account. **Note**: By default, it is set to use the location of the resource group. If you need to change this value, you can find the supported regions for your subscription via:
  - [Azure CLI](https://learn.microsoft.com/cli/azure/account?view=azure-cli-latest#az-account-list-locations)
  - PowerShell: `Get-AzLocation | Where-Object {$_.Providers -contains "Microsoft.DocumentDB"} | Select location`
- **Account Name** - Replace `YOUR_SUFFIX` with a suffix to make your Azure Cosmos DB account name unique.
- **Database Name** - Set to the default **Hotels**.
- **Sensor Container Name** - This is the container partitioned by `/DeviceId`. Set to the default **SensorEvents**.
- **Throughput** - Set to the default **400**.
- **Enable Free Tier** - This defaults to `false`. Set it to **true** if you want to use it as [the free tier account](https://learn.microsoft.com/azure/cosmos-db/free-tier).

Once those settings are set, select **Review + create**, then **Create**.

## Set up environment variables

You need 2 environment variables to run these demos.

1. Once the template deployment is complete, select **Go to resource group**.
2. Select the new Azure Cosmos DB for NoSQL account.
3. From the navigation, under **Settings**, select **Keys**. The values you need for the environment variables for the demo are here.

Create 2 environment variables to run the demos:

- `COSMOS_ENDPOINT`: set to the `URI` value on the Azure Cosmos DB account Keys blade.
- `COSMOS_KEY`: set to the Read-Write `PRIMARY KEY` for the Azure Cosmos DB for NoSQL account

Create your environment variables with the following syntax:

PowerShell:

```powershell
$env:COSMOS_ENDPOINT="YOUR_COSMOS_ENDPOINT"
$env:COSMOS_KEY="YOUR_COSMOS_READ_WRITE_PRIMARY_KEY"
```

Bash:

```bash
export COSMOS_ENDPOINT="YOUR_COSMOS_ENDPOINT"
export COSMOS_KEY="YOUR_COSMOS_KEY"
```

Windows Command:

```text
set COSMOS_ENDPOINT=YOUR_COSMOS_ENDPOINT
set COSMOS_KEY=YOUR_COSMOS_KEY
```

## Run the demo

1. Start the function app to wait for HTTP calls to simulate events and bucketing.

```bash
func start
```

To trigger the function to generate events and write bucketed entries to Cosmos DB, you can review and run Program.cs.

Open a new terminal and run the included Console App (Program.cs) which generates events saves them bucketed by device and minute:
```bash
dotnet run
```

While the Console App is running you may return to the terminal window where the function was started to see that it is writing batches to Cosmos DB.

## Querying the bucketed sensor data
Once you have run [the demo](./code/setup.md) which generates data, you can run queries directly against the event source container by using **Data Explorer** in the Azure Portal.

1. In Azure Portal, browse to you Cosmos DB resource.
2. Select **Data Explorer** in the left menu.
3. Select your container, then choose **New SQL Query**. 
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