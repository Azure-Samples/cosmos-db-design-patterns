using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Xunit;

namespace CosmosDesignPatterns.Tests.Preallocation;

// ---------------------------------------------------------------------------
// Models – mirror the preallocation pattern source models
// ---------------------------------------------------------------------------

public class ReservationDate
{
    [JsonProperty("Date")]
    public DateTime Date { get; set; }
    [JsonProperty("IsReserved")]
    public bool IsReserved { get; set; }
}

public class Room
{
    [JsonProperty("id")]
    public string? Id { get; set; }
    [JsonProperty("hotelId")]
    public string? HotelId { get; set; }
    [JsonProperty("EntityType")]
    public string EntityType { get; set; } = "room";
    [JsonProperty("Name")]
    public string? Name { get; set; }
    [JsonProperty("ReservationDates")]
    public List<ReservationDate> ReservationDates { get; set; } = [];
}

public class Hotel
{
    [JsonProperty("id")]
    public string? Id { get; set; }
    [JsonProperty("hotelId")]
    public string? HotelId { get; set; }
    [JsonProperty("EntityType")]
    public string EntityType { get; set; } = "hotel";
    [JsonProperty("Name")]
    public string? Name { get; set; }
    [JsonProperty("City")]
    public string? City { get; set; }
}

public class Reservation
{
    [JsonProperty("id")]
    public string? Id { get; set; }
    [JsonProperty("hotelId")]
    public string? HotelId { get; set; }
    [JsonProperty("EntityType")]
    public string EntityType { get; set; } = "reservation";
    [JsonProperty("RoomId")]
    public string? RoomId { get; set; }
    [JsonProperty("StartDate")]
    public DateTime StartDate { get; set; }
    [JsonProperty("EndDate")]
    public DateTime EndDate { get; set; }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Integration tests for the Preallocation design pattern.
///
/// The pattern pre-populates a <c>ReservationDates</c> array on each Room
/// document so that room availability for a given date can be found with a
/// single, efficient JOIN query.  Without preallocation, two queries (all
/// rooms + all reservations) plus client-side merging are required.
///
/// These tests verify:
///   - Hotel and Room documents can be saved and retrieved.
///   - Rooms can be pre-seeded with a <c>ReservationDates</c> array.
///   - Querying available rooms via the preallocation JOIN finds the correct results.
///   - Marking a date as reserved removes that room from availability results.
///   - Without preallocation, reservation documents track bookings separately.
/// </summary>
public class PreallocationTests : IClassFixture<EmulatorFixture>, IAsyncLifetime
{
    private readonly CosmosClient _client;
    private readonly string _databaseName = $"PreallocationTest-{Guid.NewGuid():N}";
    private Container _container = default!;

    public PreallocationTests(EmulatorFixture fixture)
    {
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        Database db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        // Partition key matches the preallocation pattern (/hotelId).
        _container = await db.CreateContainerIfNotExistsAsync("Hotel", "/hotelId");
    }

    public async Task DisposeAsync()
    {
        await _client.GetDatabase(_databaseName).DeleteAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Hotel CreateHotel(string hotelId) => new()
    {
        Id = hotelId,
        HotelId = hotelId,
        Name = "Test Hotel",
        City = "Redmond"
    };

    /// <summary>
    /// Creates a room pre-seeded with one <see cref="ReservationDate"/> per
    /// calendar day in the given range (all available initially).
    /// </summary>
    private static Room CreatePreallocatedRoom(string hotelId, string roomId, DateTime start, int days)
    {
        var room = new Room
        {
            Id = roomId,
            HotelId = hotelId,
            Name = $"Room {roomId}"
        };

        for (int i = 0; i < days; i++)
        {
            room.ReservationDates.Add(new ReservationDate
            {
                Date = start.Date.AddDays(i),
                IsReserved = false
            });
        }

        return room;
    }

    private async Task<List<Room>> QueryAvailableRoomsAsync(string hotelId, DateTime date)
    {
        // Mirrors Hotel.FindAvailableRooms in "preallocation" mode:
        // one JOIN query to find unreserved dates.
        string sql = $"""
            SELECT room
            FROM room room
            JOIN a IN room.ReservationDates
            WHERE
                a.Date >= '{date:o}' AND
                a.Date <  '{date.AddDays(1):o}' AND
                a.IsReserved = false AND
                room.hotelId = @hotelId
            """;

        var query = new QueryDefinition(sql).WithParameter("@hotelId", hotelId);
        var results = new List<Room>();

        using FeedIterator<Room> feed = _container.GetItemQueryIterator<Room>(query);
        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndRetrieve_Hotel_Succeeds()
    {
        string hotelId = $"hotel-{Guid.NewGuid():N}";
        var hotel = CreateHotel(hotelId);

        await _container.UpsertItemAsync(hotel, new PartitionKey(hotelId));

        var result = await _container.ReadItemAsync<Hotel>(hotelId, new PartitionKey(hotelId));

        Assert.Equal("Test Hotel", result.Resource.Name);
        Assert.Equal("hotel", result.Resource.EntityType);
    }

    [Fact]
    public async Task SaveAndRetrieve_PreallocatedRoom_Succeeds()
    {
        string hotelId = $"hotel-{Guid.NewGuid():N}";
        var room = CreatePreallocatedRoom(hotelId, "room-1", DateTime.UtcNow.Date, days: 7);

        await _container.UpsertItemAsync(room, new PartitionKey(hotelId));

        var result = await _container.ReadItemAsync<Room>(room.Id!, new PartitionKey(hotelId));

        Assert.Equal("room", result.Resource.EntityType);
        Assert.Equal(7, result.Resource.ReservationDates.Count);
        Assert.All(result.Resource.ReservationDates, rd => Assert.False(rd.IsReserved));
    }

    [Fact]
    public async Task QueryAvailableRooms_WithPreallocation_ReturnsAllUnreservedRooms()
    {
        string hotelId = $"hotel-{Guid.NewGuid():N}";
        DateTime queryDate = DateTime.UtcNow.Date;

        var room1 = CreatePreallocatedRoom(hotelId, "room-a1", queryDate, days: 3);
        var room2 = CreatePreallocatedRoom(hotelId, "room-a2", queryDate, days: 3);

        await _container.UpsertItemAsync(room1, new PartitionKey(hotelId));
        await _container.UpsertItemAsync(room2, new PartitionKey(hotelId));

        var available = await QueryAvailableRoomsAsync(hotelId, queryDate);

        Assert.Equal(2, available.Count);
    }

    [Fact]
    public async Task MarkingDateAsReserved_RemovesRoom_FromAvailabilityResults()
    {
        string hotelId = $"hotel-{Guid.NewGuid():N}";
        DateTime queryDate = DateTime.UtcNow.Date;

        var room = CreatePreallocatedRoom(hotelId, "room-r1", queryDate, days: 3);
        await _container.UpsertItemAsync(room, new PartitionKey(hotelId));

        // Mark today as reserved – mirrors Hotel.CreateReservationsAsync in preallocation mode.
        var targetDate = room.ReservationDates.First(rd => rd.Date == queryDate);
        targetDate.IsReserved = true;
        await _container.UpsertItemAsync(room, new PartitionKey(hotelId));

        var available = await QueryAvailableRoomsAsync(hotelId, queryDate);

        Assert.Empty(available);
    }

    [Fact]
    public async Task WithoutPreallocation_ReservationDocument_TracksBookings()
    {
        string hotelId = $"hotel-{Guid.NewGuid():N}";
        DateTime checkIn = DateTime.UtcNow.Date;

        // Without preallocation a separate Reservation document is stored per booking.
        var reservation = new Reservation
        {
            Id = $"res-{Guid.NewGuid():N}",
            HotelId = hotelId,
            RoomId = "room-np1",
            StartDate = checkIn,
            EndDate = checkIn.AddDays(1)
        };

        await _container.UpsertItemAsync(reservation, new PartitionKey(hotelId));

        // Retrieve all reservations for the hotel on the given date.
        string sql = $"""
            SELECT *
            FROM c
            WHERE
                c.EntityType = 'reservation' AND
                c.hotelId = @hotelId AND
                c.StartDate >= '{checkIn:o}' AND
                c.StartDate < '{checkIn.AddDays(1):o}'
            """;

        var query = new QueryDefinition(sql).WithParameter("@hotelId", hotelId);
        var reservations = new List<Reservation>();

        using FeedIterator<Reservation> feed = _container.GetItemQueryIterator<Reservation>(query);
        while (feed.HasMoreResults)
        {
            var page = await feed.ReadNextAsync();
            reservations.AddRange(page.Resource);
        }

        Assert.Single(reservations);
        Assert.Equal("room-np1", reservations[0].RoomId);
        Assert.Equal(checkIn, reservations[0].StartDate);
    }
}
