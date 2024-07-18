using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Preallocation
{
    public class Hotel
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
        [JsonProperty("hotelId")]
        public string? HotelId { get; set; }  //Partition Key
        public string? EntityType { get; set; }
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

        //Query the container to get list of  rooms
        async public Task<List<Room>> GetRooms(Container container)
        {
            string query = $"""
                SELECT * 
                FROM c 
                WHERE 
                    c.EntityType = 'room' AND 
                    c.hotelId = '{this.HotelId}'
             """;

            QueryDefinition queryDefinition = new QueryDefinition(query);
            using FeedIterator<Room> feed = container.GetItemQueryIterator<Room>(queryDefinition: queryDefinition);

            List<Room> items = new List<Room>();
            while (feed.HasMoreResults)
            {
                FeedResponse<Room> response = await feed.ReadNextAsync();
                items.AddRange(response);
            }

            return items;
        }

        //query room availability for a given date
        async public Task FindAvailableRooms(Container container, DateTime queryDate, string mode)
        {
            var requestCharge = 0.0;
            var executionTime = new TimeSpan();

            //Very easy to find available dates using Preallocation
            if (mode == "preallocation")
            {
                //Cosmos query to find hotel rooms 
                string query = $"""
                    SELECT 
                        a.Date, a.IsReserved, room.hotelId 
                    FROM 
                        room room 
                    JOIN a IN room.ReservationDates 
                        WHERE 
                            a.Date>= '{queryDate:o}' AND 
                            a.Date < '{queryDate.AddDays(1):o}' AND
                            a.IsReserved=false AND 
                            room.hotelId = '{this.HotelId}'
                 """;
                
                using FeedIterator<Room> feed = container.GetItemQueryIterator<Room>(new QueryDefinition(query));

                List<Room> rooms = new List<Room>();
                while (feed.HasMoreResults)
                {
                    FeedResponse<Room> response = await feed.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    rooms.AddRange(response);
                }

                Console.WriteLine($"With preallocation: {rooms.Count} room(s) available on {queryDate}, query consumed {requestCharge} RU(s) and completed in {executionTime.Milliseconds} milliseconds(s). ");
              
            }
            else  //Not using Preallocation
            {

                List<Room> availableRooms = new List<Room>();
                List<DateTime> reservedDates = new List<DateTime>();
                List<Reservation> reservations = new List<Reservation>();

                //Cosmos query without Preallocation
                //First, query all the rooms in hotel which is inefficient
                string query = $"""
                    SELECT * 
                    FROM c 
                    WHERE c.EntityType = 'room' AND c.hotelId = '{this.HotelId}'
                """;
                
                using FeedIterator<Room> feedRooms = container.GetItemQueryIterator<Room>(new QueryDefinition(query));

                while (feedRooms.HasMoreResults)
                {
                    FeedResponse<Room> response = await feedRooms.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    availableRooms.AddRange(response);
                }

                //Next run a second query to get all the reservations on the given dates
                query = $"""
                    SELECT * 
                    FROM c 
                    WHERE 
                        c.EntityType = 'reservation' AND 
                        c.hotelId = '{this.HotelId}' AND 
                        c.StartDate>= '{queryDate:o}' AND 
                        c.StartDate < '{queryDate.AddDays(1):o}'
                """;

                using FeedIterator<Reservation> feedReservations = container.GetItemQueryIterator<Reservation>(new QueryDefinition(query));

                while (feedReservations.HasMoreResults)
                {
                    FeedResponse<Reservation> response = await feedReservations.ReadNextAsync();
                    requestCharge += response.RequestCharge;
                    executionTime += response.Diagnostics.GetClientElapsedTime();
                    reservations.AddRange(response);
                }

                //Now merge the data to remove any rooms where reservations overlap the search dates
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
                Room room = rooms[i];

                //lets assume there is 1 reservation every 25 days for each room
                int noDays = 25;
                DateTime targetDate = start;

                while (targetDate < end)
                {
                    Reservation reservation = new Reservation();
                    reservation.HotelId = this.Id;
                    reservation.Id = $"reservation_{room.Id}_{targetDate.ToString("yyyyMMdd")}";
                    reservation.RoomId = $"{room.Id}";
                    reservation.StartDate = targetDate;
                    reservation.EndDate = targetDate.AddDays(1);
                    reservation.Room = room;
                                       
                    //additional preallocated data array is used to track available dates...faster
                    if (mode == "preallocation")
                    {

                        //set the available date to false...
                        foreach (var availableDate in room.ReservationDates.Where<ReservationDate>(room => room.Date == targetDate))
                            availableDate.IsReserved = true;

                        //save the room data
                        await hotelContainer.UpsertItemAsync<Room>(
                             item: room,
                             partitionKey: new PartitionKey(room.HotelId)
                        );
                    }
                    else
                    {

                        //save the reservation
                        await hotelContainer.UpsertItemAsync<Reservation>(
                            item: reservation,
                            partitionKey: new PartitionKey(reservation.HotelId)
                        );
                    }
                    targetDate = targetDate.AddDays(noDays);

                }
            }
        }

        public static Hotel CreateHotel(string id)
        {
            Hotel hotel = new Hotel();
            hotel.Id = $"hotel_{id}";
            hotel.HotelId = hotel.Id;
            hotel.Name = "Microsoft Hotels Inc";
            hotel.City = "Redmond";
            hotel.Address = "1 Microsoft Way";

            return hotel;
        }

        public static List<Room> CreateRooms(Hotel h)
        {
            List<Room> rooms = new List<Room>();

            for (int i = 0; i < maxRooms; i++)
            {
                Room room = new Room();
                room.HotelId = h.Id;
                room.Id = $"room_{i.ToString()}";
                rooms.Add(room);
            }

            return rooms;
        }
    }
}