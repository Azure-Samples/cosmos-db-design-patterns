using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.ComponentModel.Design;

namespace Cosmos_Patterns_Preallocation
{
    internal class Program
    {
        static CosmosClient? client;

        static Database? db;
        static string WithPreallocation=string.Empty;
        static string WithoutPreallocation=string.Empty;

        static async Task Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json")
               .AddJsonFile("appsettings.development.json")
               .Build();

            var client = new CosmosClient(configuration["CosmosUri"], configuration["CosmosKey"]);
            var database = configuration["Database"];
            WithPreallocation = configuration["WithPreallocation"];
            WithoutPreallocation = configuration["WithoutPreallocation"];


            db = await client.CreateDatabaseIfNotExistsAsync(
                id: database
            );


            bool exit = false;

            while (exit == false)
            {
                Console.Clear();
                Console.WriteLine($"Azure Cosmos DB Preallocation Design Pattern Demo");
                Console.WriteLine($"-----------------------------------------------------------");
                Console.WriteLine($"[1]   Create Sample Collections");
                Console.WriteLine($"[2]   Run Query with out Preallocation");
                Console.WriteLine($"[3]   Run Query with Preallocation");
                Console.WriteLine($"[4]   Exit\n");

                Console.WriteLine($"");
                Console.Write("Enter your choice (1-4): ");

                ConsoleKeyInfo result = Console.ReadKey(true);

                if (result.KeyChar == '1')
                {
                    Console.Clear();
                    Console.WriteLine("Creating objects without Preallocation...");
                    await CreateWithNoPreallocationAsync(WithoutPreallocation);

                    Console.WriteLine("Creating objects with Preallocation...");
                    await CreateWithPreallocationAsync(WithPreallocation);

                }
                else if (result.KeyChar == '2')
                {                   
                    QueryContainerAsync(WithoutPreallocation,"nopreallocation").GetAwaiter().GetResult();
                }
                else if (result.KeyChar == '3')
                {                    
                    QueryContainerAsync(WithPreallocation, "preallocation").GetAwaiter().GetResult();
                }
                else if (result.KeyChar == '4')
                {
                    Console.WriteLine("Goodbye!");
                    exit = true;
                }
            }
        }


        async static Task<Container> CreateContainerIfNotExistsAsync(string containerName)
        {
            return await db.CreateContainerIfNotExistsAsync(
                           id: containerName,
                           partitionKeyPath: "/hotelId",
                           throughput: 400
                       );
        }

        async static Task CreateWithNoPreallocationAsync(string containerName)
        {

            Container hotelContainer = await CreateContainerIfNotExistsAsync(containerName);

            string mode = "nopreallocation";

            //create the hotel
            Hotel h = Hotel.CreateHotel("1");

            //save the hotel...
            await hotelContainer.UpsertItemAsync<Hotel>(
                item: h,
                partitionKey: new PartitionKey(h.HotelId)
            );

            //create the rooms
            List<Room> rooms = Hotel.CreateRooms(h);

            foreach (Room r in rooms)
                await hotelContainer.UpsertItemAsync<Room>(
                    item: r,
                    partitionKey: new PartitionKey(r.HotelId)
                );

            //start today then go to next 365 days 
            DateTime start = DateTime.SpecifyKind(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), DateTimeKind.Utc); 

            DateTime end = DateTime.Today.AddDays(365);

            //create random reservations.
            await h.CreateReservationsAsync(start,end,mode, hotelContainer);

        }
        
        async static Task QueryContainerAsync(string containerName,  string mode)
        {
            DateTime reservationSearchDate;
            Console.Clear();
            Console.WriteLine("Specify reservation date. (please provide a date within next 365 days in MM-dd-yyyy format)");
            string input = Console.ReadLine();

            if (input == "")
                reservationSearchDate = System.DateTime.Today;
            else
                reservationSearchDate = DateTime.ParseExact(input, "MM-dd-yyyy", CultureInfo.InvariantCulture);


            //create the hotel
            Hotel h = Hotel.CreateHotel("1");

            Container hotelContainer = await CreateContainerIfNotExistsAsync(containerName);
            //find an available room for a date using reservations...
            await h.FindAvailableRooms(hotelContainer, reservationSearchDate, mode);
           
        }

        async static Task CreateWithPreallocationAsync(string containerName)
        {

            Container hotelContainer = await CreateContainerIfNotExistsAsync(containerName);

            string mode = "preallocation";

            //create the hotel
            Hotel h = Hotel.CreateHotel("1");

            //save the hotel...
            try
            {
                //save the hotel...
                await hotelContainer.UpsertItemAsync<Hotel>(
                    item: h,
                    partitionKey: new PartitionKey(h.Id)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            //create the rooms
            List<Room> rooms = Hotel.CreateRooms(h);

            foreach (Room r in rooms)
                await hotelContainer.UpsertItemAsync<Room>(
                    item: r,
                    partitionKey: new PartitionKey(r.HotelId)
                );

            // go 365 days from now
            DateTime start = DateTime.SpecifyKind(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), DateTimeKind.Utc);

            DateTime end = DateTime.Today.AddDays(365);

            //add all the days for the year which can be queried later.
            foreach (Room r in rooms)
                {
                    int count = 0;

                    while (start.AddDays(count) < end)
                    {
                        r.ReservationDates.Add(new ReservationDate { Date = start.AddDays(count), IsReserved = false });
                        count++;
                    }

                    try
                    {
                        await hotelContainer.UpsertItemAsync<Room>(
                            item: r,
                            partitionKey: new PartitionKey(r.HotelId)
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

            //create random reservations.
            await h.CreateReservationsAsync(start,end, "preallocation", hotelContainer);

        }
    }
}