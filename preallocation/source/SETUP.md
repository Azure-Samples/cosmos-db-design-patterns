# Setup

This template will create an Azure Cosmos DB for NoSQL account with a database named `CosmosPatterns` with a container named `HotelApp`.

The suggested account name includes 'YOUR_SUFFIX'. Change that to a suffix to make your account name unique.

The Azure Cosmos DB for NoSQL account will automatically be created with the region of the selected resource group.

---

**This link will work if this is a public repo.**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsolliancenet%2Fcosmos-db-nosql-modeling%2Fmain%2FCosmos_Patterns_Preallocation%2Fsetup%2Fazuredeploy.json)

**For the private repo**

1. [Create a custom template deployment](https://portal.azure.com/#create/Microsoft.Template/).
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
- **Database Name** - Set to the default **CosmosPatterns**.
- **Hotel App Container Name** - This is the container partitioned by `/Id`.
- **Throughput** - Set to the default **400**.
- **Enable Free Tier** - This defaults to `false`. Set it to **true** if you want to use it as [the free tier account](https://learn.microsoft.com/azure/cosmos-db/free-tier).

Once those settings are set, select **Review + create**, then **Create**.

## Updating Azure Cosmos DB URI and Key in Code

1. Once the template deployment is complete, select **Go to resource group**.
2. Select the new Azure Cosmos DB for NoSQL account.
3. From the navigation, under **Settings**, select **Keys**.

Update the  following in the **appsettings.json**  before you run the code:

- `CosmosUri`: Set to the `URI` value on the Azure Cosmos DB account Keys blade.
- `CosmosKey`: Set to the Read-Write `PRIMARY KEY` for the Azure Cosmos DB for NoSQL account

## Run the demo

1. Review the `program.cs` file.
2. Notice the following code snippets

```csharp
Hotel h = Hotel.CreateHotel("1");
```

> NOTE: This will create a new hotel object with the id.

```csharp
List<Room> rooms = Hotel.CreateRooms(h);
```

> NOTE: This will create a set of rooms for a hotel, you can change the numbner of rooms (set to 10) to create by modifying the appropriate property in the class.

3. From Visual Studio Code, start the app by running the following:

  ```bash
  dotnet build
  dotnet run
  ```

4. From Visual Studio, press **F5** to start the application.

5. Select Option 1 in the console application to create the containers and populate data in them. The code will create two containers.  One that contains reservations that are used to determine open dates and another hotel that uses pre-allocation of dates.

	1. In Azure Portal, browse to you Cosmos DB resource.
	2. Select **Data Explorer** in the left menu.
	3. Review the data in both  container, notice that the 'Reservation' documents is not used in the *HotelApp_containerWithPreallocation*.

6. Select Option 2 to run the query with out any Preallocation. Provide a date in DD--MM-YYYY format.
7. Select Option 3 to run the same query using Preallocation. Provide a date in DD--MM-YYYY format.
8. Compare the code for both Step#6 and Step#7. Notice that Pre-allocation allows for a much simpler design for queries and logic versus other approaches however it can come at the cost of a larger document in storage and memory given the pre-allocation of the data.
