using System.Text.Json;
using Bogus;
using DataUploader.Models;
using Microsoft.Azure.Cosmos;
using Spectre.Console;
using Spectre.Console.Json;
using Console = Spectre.Console.AnsiConsole;

namespace DataUploader.Services;

internal sealed class HotelService
{
    private readonly Container __hotelRoomsContainer;

    public HotelService(Container hotelRoomsContainer)
    {
        __hotelRoomsContainer = hotelRoomsContainer;
    }

    public async Task GenerateHotelRoomsAsync()
    {
        string hotelId = await GenerateHotelAsync();

        // Property-based
        await GeneratePropertyBasedAttributeHotelRoomsAsync(hotelId);

        // Array-based
        await GenerateArrayBasedAttributeHotelRoomsAsync(hotelId);
    }

    private async Task<string> GenerateHotelAsync()
    {
        Console.MarkupLine($"[red italic]Creating [underline]1[/] hotel item...[/]");
        await Task.Delay(1500);

        Hotel hotel = new Faker<Hotel>()
            .CustomInstantiator(f =>
            {
                string identifier = $"{f.Random.Guid()}";
                return new Hotel(
                    Id: identifier,
                    HotelId: identifier,
                    Name: f.Company.CompanyName(),
                    City: f.Address.City(),
                    Rooms: Enumerable.Empty<Room>().ToList()
                );
            })
            .Generate();

        await __hotelRoomsContainer.UpsertItemAsync<Hotel>(
            item: hotel,
            partitionKey: new PartitionKey(hotel.HotelId)
        );

        string itemJson = JsonSerializer.Serialize(hotel);
        Console.Write(
            new Panel(
                new JsonText(itemJson)
            )
                .Header($"[teal bold]Hotel created[/]")
                .RoundedBorder()
                .Expand()
                .BorderColor(Color.Teal)
        );

        return hotel.HotelId;
    }

    private async Task GeneratePropertyBasedAttributeHotelRoomsAsync(string hotelId)
    {
        int count = 5;

        Console.MarkupLine($"[red italic]Creating [underline]{count}[/] hotel room items with [bold]property-based attributes[/]...[/]");
        await Task.Delay(1500);

        IReadOnlyCollection<AttributePropertyRoom> rooms = new Faker<AttributePropertyRoom>()
            .CustomInstantiator(f =>
                {
                    return new AttributePropertyRoom(
                        Id: $"{f.Random.Guid()}",
                        HotelId: hotelId,
                        LeaseId: $"{f.Random.Guid()}",
                        LeasedUntil: f.Date.Future(),
                        MaxGuests: f.Random.Int(1, 4),
                        PriceUSD: f.Random.Decimal(200, 1000),
                        PriceEUR: f.Random.Decimal(200, 1000),
                        SizeSquareMeters: f.Random.Int(100, 300),
                        SizeSquareFeet: f.Random.Int(100, 300)
                    );
                })
            .Generate(count)
            .AsReadOnly();

        List<Task> upsertTasks = new();
        foreach (var room in rooms)
        {
            upsertTasks.Add(
                __hotelRoomsContainer.UpsertItemAsync<AttributePropertyRoom>(
                    item: room,
                    partitionKey: new PartitionKey(room.HotelId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);

        Console.Write(
            new Panel(
                new Rows(
                    rooms.Select(r => new Markup($"New room: [underline]{r.Id}[/]")).ToArray()
                )
            )
                .Header($"[teal bold]Rooms created[/]")
                .RoundedBorder()
                .BorderColor(Color.Teal)
        );

        Console.MarkupLine("[red italic]Performing two queries on all attributes using multiple [underline]OR[/] statements...[/]");
        await Task.Delay(1500);

        // Query price
        {
            string queryString = "SELECT VALUE r FROM rooms r WHERE r.priceEUR >= @price OR r.priceUSD >= @price";

            int inputPrice = 750;
            var query = new QueryDefinition(queryString)
                .WithParameter("@price", inputPrice);

            using FeedIterator<AttributePropertyRoom> feed = __hotelRoomsContainer.GetItemQueryIterator<AttributePropertyRoom>(query);

            List<AttributePropertyRoom> items = new();
            while (feed.HasMoreResults)
            {
                FeedResponse<AttributePropertyRoom> response = await feed.ReadNextAsync();

                items.AddRange(response.Resource);
            }

            string listJson = JsonSerializer.Serialize(items);
            Console.Write(
                new Panel(
                    new Rows(
                        new Markup($"[teal][italic]@price[/]: {inputPrice}[/]"),
                        new Markup($"[teal]{queryString}[/]"),
                        new Markup($"Matched [underline]{items.Count}[/] rooms"),
                        new JsonText(listJson)
                    )
                )
                    .Header("[bold]Query results[/]")
                    .RoundedBorder()
                    .Expand()
                    .BorderColor(Color.Teal)
            );
        }

        await Task.Delay(1500);

        // Query size
        {
            string queryString = "SELECT VALUE r FROM rooms r WHERE r.sizeSquareMeters >= @size OR r.sizeSquareFeet >= @size";

            int inputSize = 200;
            var query = new QueryDefinition(queryString)
                .WithParameter("@size", inputSize);

            using FeedIterator<AttributePropertyRoom> feed = __hotelRoomsContainer.GetItemQueryIterator<AttributePropertyRoom>(query);

            List<AttributePropertyRoom> items = new();
            while (feed.HasMoreResults)
            {
                FeedResponse<AttributePropertyRoom> response = await feed.ReadNextAsync();

                items.AddRange(response.Resource);
            }

            string listJson = JsonSerializer.Serialize(items);
            Console.Write(
                new Panel(
                    new Rows(
                        new Markup($"[teal][italic]@size[/]: {inputSize}[/]"),
                        new Markup($"[teal]{queryString}[/]"),
                        new Markup($"Matched [underline]{items.Count}[/] rooms"),
                        new JsonText(listJson)
                    )
                )
                    .Header("[bold]Query results[/]")
                    .RoundedBorder()
                    .Expand()
                    .BorderColor(Color.Teal)
            );
        }
    }

    private async Task GenerateArrayBasedAttributeHotelRoomsAsync(string hotelId)
    {
        int count = 5;

        Console.MarkupLine($"[red italic]Creating [underline]{count}[/] hotel room items with [bold]array-based attributes[/]...[/]");
        await Task.Delay(1500);

        IReadOnlyCollection<AttributeArrayRoom> rooms = new Faker<AttributeArrayRoom>()
            .CustomInstantiator(f =>
                {
                    return new AttributeArrayRoom(
                        Id: $"{f.Random.Guid()}",
                        HotelId: hotelId,
                        LeaseId: $"{f.Random.Guid()}",
                        LeasedUntil: f.Date.Future(),
                        MaxGuests: f.Random.Int(1, 4),
                        Prices: new List<RoomPrice>
                        {
                            new RoomPrice("USD", f.Random.Decimal(200, 1000)),
                            new RoomPrice("EUR", f.Random.Decimal(200, 1000))
                        },
                        Sizes: new List<RoomSize>
                        {
                            new RoomSize("SquareMeters", f.Random.Int(100, 300)),
                            new RoomSize("SquareFeet", f.Random.Int(100, 300))
                        }
                    );
                })
            .Generate(count)
            .AsReadOnly();

        List<Task> upsertTasks = new();
        foreach (var room in rooms)
        {
            upsertTasks.Add(
                __hotelRoomsContainer.UpsertItemAsync<AttributeArrayRoom>(
                    item: room,
                    partitionKey: new PartitionKey(room.HotelId)
                )
            );
        }
        await Task.WhenAll(upsertTasks);

        Console.Write(
            new Panel(
                new Rows(
                    rooms.Select(r => new Markup($"New room: [underline]{r.Id}[/]")).ToArray()
                )
            )
                .Header($"[teal bold]Rooms created[/]")
                .RoundedBorder()
                .BorderColor(Color.Teal)
        );

        Console.MarkupLine("[red italic]Performing two queries on attributes using simple [underline]JOIN[/] statements and comparison operators...[/]");
        await Task.Delay(1500);

        // Query price
        {
            string queryString = $"SELECT VALUE r FROM room r JOIN p IN r.prices WHERE p.price >= @price";

            int inputPrice = 750;
            var query = new QueryDefinition(queryString)
                .WithParameter("@price", inputPrice);

            using FeedIterator<AttributeArrayRoom> feed = __hotelRoomsContainer.GetItemQueryIterator<AttributeArrayRoom>(query);

            List<AttributeArrayRoom> items = new();
            while (feed.HasMoreResults)
            {
                FeedResponse<AttributeArrayRoom> response = await feed.ReadNextAsync();

                items.AddRange(response.Resource);
            }

            string listJson = JsonSerializer.Serialize(items);
            Console.Write(
                new Panel(
                    new Rows(
                        new Markup($"[teal][italic]@price[/]: {inputPrice}[/]"),
                        new Markup($"[teal]{queryString}[/]"),
                        new Markup($"Matched [underline]{items.Count}[/] rooms"),
                        new JsonText(listJson)
                    )
                )
                    .Header("[bold]Query results[/]")
                    .RoundedBorder()
                    .Expand()
                    .BorderColor(Color.Teal)
            );
        }

        await Task.Delay(1500);

        // Query size
        {
            string queryString = $"SELECT VALUE r FROM room r JOIN s IN r.sizes WHERE s.size >= @size";

            int inputSize = 200;
            var query = new QueryDefinition(queryString)
                .WithParameter("@size", inputSize);

            using FeedIterator<AttributeArrayRoom> feed = __hotelRoomsContainer.GetItemQueryIterator<AttributeArrayRoom>(query);

            List<AttributeArrayRoom> items = new();
            while (feed.HasMoreResults)
            {
                FeedResponse<AttributeArrayRoom> response = await feed.ReadNextAsync();

                items.AddRange(response.Resource);
            }

            string listJson = JsonSerializer.Serialize(items);
            Console.Write(
                new Panel(
                    new Rows(
                        new Markup($"[teal][italic]@size[/]: {inputSize}[/]"),
                        new Markup($"[teal]{queryString}[/]"),
                        new Markup($"Matched [underline]{items.Count}[/] rooms"),
                        new JsonText(listJson)
                    )
                )
                    .Header("[bold]Query results[/]")
                    .RoundedBorder()
                    .Expand()
                    .BorderColor(Color.Teal)
            );
        }
    }
}