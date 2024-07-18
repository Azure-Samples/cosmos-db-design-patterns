using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Preallocation.Options;
using System.Globalization;

namespace Preallocation
{
    internal class Program
    {
        static CosmosClient? _client;
        static Container? _withPreallocationContainer;
        static Container? _withoutPreallocationContainer;
        static Cosmos? _config;

        static async Task Main(string[] args)
        {
            IConfigurationBuilder configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true);

            _config = configuration
                .Build()
                .Get<Cosmos>();


            _client = new CosmosClient(_config?.CosmosUri, _config?.CosmosKey);           

            await InitializeDatabase();

            bool exit = false;

            while (exit == false)
            {
                Console.Clear();
                Console.WriteLine($"Azure Cosmos DB Preallocation Design Pattern Demo");
                Console.WriteLine($"-----------------------------------------------------------");
                Console.WriteLine($"[1]   Load Hotel and Room Data");
                Console.WriteLine($"[2]   Run Query with out Preallocation");
                Console.WriteLine($"[3]   Run Query with Preallocation");
                Console.WriteLine($"[4]   Reset Data");
                Console.WriteLine($"[5]   Exit\n");

                Console.WriteLine($"");
                Console.Write("Enter your choice (1-5): ");

                ConsoleKeyInfo result = Console.ReadKey(true);

                if (result.KeyChar == '1')
                {
                    Console.Clear();
                    Console.WriteLine("Creating objects without Preallocation...");
                    await CreateWithNoPreallocationAsync();

                    Console.WriteLine("Creating objects with Preallocation...");
                    await CreateWithPreallocationAsync();

                }
                else if (result.KeyChar == '2')
                {                   
                    QueryContainerAsync(_withoutPreallocationContainer!,"nopreallocation").GetAwaiter().GetResult();
                }
                else if (result.KeyChar == '3')
                {                    
                    QueryContainerAsync(_withPreallocationContainer!, "preallocation").GetAwaiter().GetResult();
                }
                else if (result.KeyChar == '4')
                {
                    await ResetData();
                    Console.WriteLine("Data reset for containers");
                    Console.WriteLine("Press Any Key to Continue..");
                    Console.ReadKey();
                }
                else if (result.KeyChar == '5')
                {
                    Console.WriteLine("Goodbye!");
                    exit = true;
                }
            }
        }

        async static Task InitializeDatabase()
        {
            Database database = await _client!.CreateDatabaseIfNotExistsAsync(id: _config?.DatabaseName!);

            _withPreallocationContainer = await database.CreateContainerIfNotExistsAsync(
                id: _config?.WithPreallocation!,
                partitionKeyPath: "/hotelId");

            _withoutPreallocationContainer = await database.CreateContainerIfNotExistsAsync(
                id: _config?.WithoutPreallocation!,
                partitionKeyPath: "/hotelId");
        }

        async static Task CreateWithNoPreallocationAsync()
        {

            string mode = "nopreallocation";

            //create the hotel
            Hotel hotel = Hotel.CreateHotel("1");

            //save the hotel...
            await _withoutPreallocationContainer!.CreateItemAsync<Hotel>(
                item: hotel,
                partitionKey: new PartitionKey(hotel.HotelId)
            );

            //create the rooms
            List<Room> rooms = Hotel.CreateRooms(hotel);

            foreach (Room room in rooms)
                await _withoutPreallocationContainer.CreateItemAsync<Room>(
                    item: room,
                    partitionKey: new PartitionKey(room.HotelId)
                );

            //start today then go to next 365 days 
            DateTime start = DateTime.SpecifyKind(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), DateTimeKind.Utc); 

            DateTime end = DateTime.Today.AddDays(365);

            //create random reservations.
            await hotel.CreateReservationsAsync(start, end, mode, _withoutPreallocationContainer);

        }

        async static Task CreateWithPreallocationAsync()
        {
            //create a hotel
            Hotel hotel = Hotel.CreateHotel("1");

            //save the hotel...
            await _withPreallocationContainer!.CreateItemAsync<Hotel>(
                item: hotel,
                partitionKey: new PartitionKey(hotel.Id)
            );
            
            //create the rooms
            List<Room> rooms = Hotel.CreateRooms(hotel);

            foreach (Room room in rooms)
                await _withPreallocationContainer!.CreateItemAsync<Room>(
                    item: room,
                    partitionKey: new PartitionKey(room.HotelId)
                );

            // go 365 days from now
            DateTime start = DateTime.SpecifyKind(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), DateTimeKind.Utc);

            DateTime end = DateTime.Today.AddDays(365);

            //Preallocate all the days for the year which can be queried to reserve a room later.
            foreach (Room room in rooms)
            {
                int count = 0;

                while (start.AddDays(count) < end)
                {
                    room.ReservationDates.Add(new ReservationDate { Date = start.AddDays(count), IsReserved = false });
                    count++;
                }

                try
                {
                    //Update the room with the preallocated data
                    await _withPreallocationContainer!.ReplaceItemAsync<Room>(
                        item: room,
                        id: room.Id,
                        partitionKey: new PartitionKey(room.HotelId)
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            //create random reservations.
            await hotel.CreateReservationsAsync(start, end, "preallocation", _withPreallocationContainer!);

        }

        async static Task QueryContainerAsync(Container hotelContainer,  string mode)
        {
            DateTime reservationSearchDate;
            Console.Clear();
            Console.WriteLine("Specify reservation date. (please provide a date within next 365 days in MM-dd-yyyy format)");
            string input = Console.ReadLine()!;

            if (input == "")
                reservationSearchDate = System.DateTime.Today;
            else
                reservationSearchDate = DateTime.ParseExact(input, "MM-dd-yyyy", CultureInfo.InvariantCulture);


            //create the hotel
            Hotel hotel = Hotel.CreateHotel("1");

            //find an available room for a date using reservations...
            await hotel.FindAvailableRooms(hotelContainer, reservationSearchDate, mode);
           
        }

        async static Task ResetData()
        {

            await _withPreallocationContainer!.DeleteContainerAsync();
            await _withoutPreallocationContainer!.DeleteContainerAsync();

            await InitializeDatabase();
        }

        
    }
}