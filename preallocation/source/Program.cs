using Microsoft.Azure.Cosmos;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Cosmos_Patterns_Preallocation
{
    internal class Program
    {
        static CosmosClient? client;

        static Database? db;

        static Container? hotelContainer;

        static string partitionKeyPath = "/hotelId";

        static string databaseName = "HotelDB";

        static async Task Main(string[] args)
        {
            client = new(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!);

            db = await client.CreateDatabaseIfNotExistsAsync(
                id: databaseName
            );

            hotelContainer = await db.CreateContainerIfNotExistsAsync(
                id: "Hotels",
                partitionKeyPath: partitionKeyPath,
                throughput: 400
            );

            Console.WriteLine("Creating the no-preallocation objects...");

            //No preallocation...
            await NoPreallocation();

            Console.WriteLine("Review the objects in the database, press ENTER to continue");

            Console.ReadLine();

            Console.WriteLine("Creating the preallocation objects...");

            //With preallocation...
            await Preallocation();

            Console.WriteLine("Review the objects in the database, press ENTER to continue");

            Console.ReadLine();
        }

        async static Task NoPreallocation()
        {
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

            //start beginning of year and then go 365 days from now
            DateTime start = DateTime.Parse(DateTime.Now.ToString("01/01/yyyy"));
            DateTime end = DateTime.Parse(DateTime.Now.AddDays(365).ToString("MM/dd/yyyy"));

            //create random reservations.
            await h.CreateReservations(start, mode, hotelContainer);

            //find an available room for a date using reservations...
            List<Room> availableRooms = await h.FindAvailableRooms(hotelContainer, DateTime.Now, DateTime.Now.AddDays(7), mode);

            Console.WriteLine($"Available Rooms for dates [{start} to {end}]: {availableRooms.Count}");
        }

        

        async static Task Preallocation()
        {
            string mode = "preallocation";

            //create the hotel
            Hotel h = Hotel.CreateHotel("2");

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

            //start beginning of year and then go 365 days from now
            DateTime start = DateTime.Parse(DateTime.Now.ToString("01/01/yyyy"));
            DateTime end = DateTime.Parse(DateTime.Now.AddDays(365).ToString("MM/dd/yyyy"));

            //add all the days for the year which can be queried later.
            foreach (Room r in rooms)
            {
                int count = 0;

                while (start.AddDays(count) < end)
                {
                    r.AvailableDates.Add(new AvailableDate { Date = start.AddDays(count), IsAvailable = true });

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
            await h.CreateReservations(start, "preallocation", hotelContainer);

            //find an available room for a date using the preallocated data array
            List<Room> availableRooms = await h.FindAvailableRooms(hotelContainer, DateTime.Now, DateTime.Now.AddDays(7), mode);

            Console.WriteLine($"Available Rooms for dates [{start} to {end}]: {availableRooms.Count}");
        }
    }
}