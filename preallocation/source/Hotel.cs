using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Cosmos_Patterns_Preallocation
{
    public class Hotel
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("_etag")]
        public string? ETag { get; set; }

        public string? EntityType { get; set; }
        public string? LeaseId { get; set; }
        public DateTime? LeasedUntil { get; set; }

        [JsonProperty("hotelId")]
        public string? HotelId { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public int Rating { get; set; }
        public int AvailableRooms { get; set; }

        public List<Room> Rooms { get; set; }

        public ICollection<Reservation> Reservations { get; set; }

        static int maxRooms = 10;

        public Hotel()
        {
            Rooms = new List<Room>();
            Reservations = new List<Reservation>();

            EntityType = "hotel";
        }

        //query db to get list of  rooms
        async public Task<List<Room>> GetRooms(Container c)
        {
            string query = $"select * from c where c.EntityType = 'room' and c.hotelId = '{this.HotelId}'";

            QueryDefinition qd = new QueryDefinition(query);

            List<Room> items = new List<Room>();

            using FeedIterator<Room> feed = c.GetItemQueryIterator<Room>(queryDefinition: qd);

            while (feed.HasMoreResults)
            {
                FeedResponse<Room> response = await feed.ReadNextAsync();

                items.AddRange(response);
            }

            return items;
        }

        //query room  availability for a given date
        async public Task FindAvailableRooms(Container c, DateTime queryDate, string mode)
        {
            var requestCharge = 0.0;
            var executionTime = new TimeSpan();

            //very easy to find available dates...
            if (mode == "preallocation")
            {
                List<Room> items = new List<Room>();

                //do a cosmos query that finds all hotel rooms 
                string query = $"SELECT a.Date, a.IsReserved, r.hotelId FROM room r JOIN a IN r.ReservationDates WHERE a.Date>= '{queryDate:o}' AND a.Date < '{queryDate.AddDays(1):o}' and a.IsReserved=false and r.hotelId = '{this.HotelId}'";
                using FeedIterator<Room> feed = c.GetItemQueryIterator<Room>(new QueryDefinition(query));
 
                while (feed.HasMoreResults)
                {
                    FeedResponse<Room> response = await feed.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    items.AddRange(response);
                }


                Console.WriteLine($"With preallocation: {items.Count} room(s) available on {queryDate}, query consumed {requestCharge} RU(s) and completed in {executionTime.Milliseconds} milliseconds(s). ");
               
                //return items;
            }
            else
            {


                List<Room> availableRooms = new List<Room>();
                List<DateTime> reservedDates = new List<DateTime>();
                List<Reservation> reservations = new List<Reservation>();

                //get all the rooms in hotel
                string query = $"select * from c where c.EntityType = 'room' and c.hotelId = '{this.HotelId}'";
                using FeedIterator<Room> feedRooms = c.GetItemQueryIterator<Room>(new QueryDefinition(query));

                while (feedRooms.HasMoreResults)
                {
                    FeedResponse<Room> response = await feedRooms.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    availableRooms.AddRange(response);
                }

                //get all the reservations on the given date
                query = $"select * from c where c.EntityType = 'reservation' and c.hotelId = '{this.HotelId}' and c.StartDate>= '{queryDate:o}' AND c.StartDate < '{queryDate.AddDays(1):o}'";
                using FeedIterator<Reservation> feedReservations = c.GetItemQueryIterator<Reservation>(new QueryDefinition(query));

                while (feedReservations.HasMoreResults)
                {
                    FeedResponse<Reservation> response = await feedReservations.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    reservations.AddRange(response);
                }

                //merge reservation with to remove any rooms where reservations overlap the search dates
                foreach (Reservation r in reservations)
                {
                    //if room reserved on the the give date
                    if (queryDate == r.StartDate)
                    {
                        //remove from available list
                        availableRooms.Remove(availableRooms.Where(r1 => r1.Id == r.RoomId).First()); 
                    }
                }
                Console.WriteLine($"Without preallocation: {availableRooms.Count} room(s) available on {queryDate}, query consumed {requestCharge} RU(s) and completed in {executionTime.Milliseconds} milliseconds(s). ");
                //return availableRooms;
            }

            Console.WriteLine("");
            Console.WriteLine("Press any key to continue.");
            Console.ReadLine();

        }

        public async Task CreateReservationsAsync(DateTime start, DateTime end, string mode, Container hotelContainer)
        {          

            List<Room> rooms = await this.GetRooms(hotelContainer);

            //create some random reservations and remove available dates
            for (int i = 0; i < rooms.Count; i++)
            {
                Room r = rooms[i];

                //lets assume there  is 1 reservation every 25 days for each room
                int noDays = 25;
                DateTime targetDate = start;
                while (targetDate < end)
                {
                    Reservation res = new Reservation();
                    res.HotelId = this.Id;
                    res.Id = $"reservation_{r.Id}_{targetDate.ToString("yyyyMMdd")}";
                    res.RoomId = $"{r.Id}";
                    res.StartDate = targetDate;
                    res.EndDate = targetDate.AddDays(1);
                    res.Room = r;
                                       

                    //additional preallocated data array is used to track available dates...faster
                    if (mode == "preallocation")
                    {
                        //set the available date to false...
                        foreach (var ad in r.ReservationDates.Where<ReservationDate>(r => r.Date == targetDate))
                            ad.IsReserved = true;


                        //save the room data
                        await hotelContainer.UpsertItemAsync<Room>(
                             item: r,
                             partitionKey: new PartitionKey(r.HotelId)
                         );
                    }
                    else
                    {
                        //save the reservation
                        await hotelContainer.UpsertItemAsync<Reservation>(
                            item: res,
                            partitionKey: new PartitionKey(res.HotelId)
                        );
                    }
                    targetDate = targetDate.AddDays(noDays);

                }

            }
        }


        public static Hotel CreateHotel(string id)
        {
            Hotel h = new Hotel();
            h.Id = $"hotel_{id}";
            h.HotelId = h.Id;
            h.Name = "Microsoft Hotels Inc";
            h.City = "Redmond";
            h.Address = "1 Microsoft Way";

            return h;
        }

        public static List<Room> CreateRooms(Hotel h)
        {
            List<Room> rooms = new List<Room>();

            for (int i = 0; i < maxRooms; i++)
            {
                Room r = new Room();
                r.HotelId = h.Id;
                r.Id = $"room_{i.ToString()}";
                rooms.Add(r);
            }

            return rooms;
        }
    }
}