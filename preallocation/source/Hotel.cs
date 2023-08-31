using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
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

        public Hotel()
        {
            Rooms = new List<Room>();
            Reservations = new List<Reservation>();

            EntityType = "hotel";
        }

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

        async public Task<List<Room>> FindAvailableRooms(Container c, DateTime start, DateTime end, string mode)
        {
            //ensure start is less than end...invalid, no rooms!
            if (start > end)
                return new List<Room>();

            //number of days of the stay.
            int noDates = (end - start).Days;

            //very easy to find available dates...
            if (mode == "preallocation")
            {
                //do a cosmos query that finds hotel rooms that are availabel between two dates...
                string query = $"select * from c where c.hotelId = '{this.HotelId}' and c.AvailableDates.IsAvailable = 1 and c.AvailableDates.Date >= '{start.ToString("yyyy-MM-dd")}' and c.AvailableDates.Date <= '{end.ToString("yyyy-MM-dd")}'";

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

            //very difficult to find available dates...
            if (mode == "nopreallocation")
            {
                //get all the reservations
                //do a cosmos query that finds hotel rooms that are availabel between two dates...
                string query = $"select * from c where c.EntityType = 'room' and c.hotelId = '{this.HotelId}'";

                QueryDefinition qd = new QueryDefinition(query);

                List<Room> availableRooms = new List<Room>();

                using FeedIterator<Room> feed = c.GetItemQueryIterator<Room>(queryDefinition: qd);

                while (feed.HasMoreResults)
                {
                    FeedResponse<Room> response = await feed.ReadNextAsync();

                    availableRooms.AddRange(response);
                }

                //get all the reservations
                //do a cosmos query that finds hotel rooms that are availabel between two dates...
                query = $"select * from c where c.EntityType = 'reservation' and c.StartDate >= '{DateTime.Now}' and c.hotelId = '{this.HotelId}'";

                List<AvailableDate> availableDates = new List<AvailableDate>();

                List<Reservation> reservations = new List<Reservation>();

                using FeedIterator<Reservation> feed2 = c.GetItemQueryIterator<Reservation>(queryDefinition: qd);

                while (feed2.HasMoreResults)
                {
                    FeedResponse<Reservation> response = await feed2.ReadNextAsync();

                    reservations.AddRange(response);
                }

                //remove any rooms where reservations overlap the search dates
                foreach (Reservation r in reservations)
                {
                    bool validRoom = true;

                    //the search start date is after both start and end date of reservation...that's ok
                    if (start > r.StartDate && start > r.EndDate)
                        validRoom = true;

                    //the search end date is after both start and end date of reservation...that's ok
                    if (end > r.StartDate && end > r.EndDate)
                        validRoom = true;

                    //if the search end date is less than the end date and greater than the start date...that's not ok..
                    if (end < r.EndDate && end > r.StartDate)
                        validRoom = false;

                    //if the search start date is less than the end date and greater than the start date...that's not ok..
                    if (start < r.EndDate && start > r.StartDate)
                        validRoom = false;

                    if (!validRoom)
                    {
                        availableRooms.Remove(availableRooms.Where(r1 => r1.Id == r.RoomId).First());
                    }

                }

                return availableRooms;
            }

            return new List<Room>();
        }

        public async Task CreateReservations(DateTime start, string mode, Container hotelContainer)
        {
            Random rand = new Random();

            List<Room> rooms = await this.GetRooms(hotelContainer);

            //create some random reservations and remove available dates
            for (int i = 0; i < rooms.Count; i++)
            {
                var query = from Room room in rooms where room.Id == $"room_{i.ToString()}" select room;

                foreach (Room r in query)
                {
                    //create random reservations
                    int noDays = rand.Next(365);
                    DateTime targetDate = start.AddDays(noDays);

                    //reservations are used to track available dates...very slow
                    if (mode == "nopreallocation")
                    {
                        Reservation res = new Reservation();
                        res.HotelId = this.Id;
                        res.Id = $"reservation_{r.Id}_{targetDate.ToString("yyyyMMdd")}";
                        res.RoomId = $"{r.Id}";
                        res.StartDate = targetDate;
                        res.EndDate = targetDate.AddDays(1);
                        res.Room = r;

                        //save the reservation
                        await hotelContainer.UpsertItemAsync<Reservation>(
                            item: res,
                            partitionKey: new PartitionKey(res.HotelId)
                        );
                    }

                    //preallocated data array is used to track available dates...faster
                    if (mode == "preallocation")
                    {
                        //set the available date to false...
                        foreach (var ad in r.AvailableDates.Where<AvailableDate>(r => r.Date == targetDate))
                            ad.IsAvailable = false;

                        //save the room data
                        await hotelContainer.UpsertItemAsync<Room>(
                            item: r,
                            partitionKey: new PartitionKey(r.HotelId)
                        );
                    }
                }
            }
        }

        static int maxRooms = 10;

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