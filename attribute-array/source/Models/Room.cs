namespace DataUploader.Models;

internal sealed record Room();

internal sealed record AttributePropertyRoom(
    string Id,
    string HotelId,
    string LeaseId,
    DateTime LeasedUntil,
    int MaxGuests,
    decimal PriceUSD,
    decimal PriceEUR,
    int SizeSquareMeters,
    int SizeSquareFeet
)
{
    public string EntityType { get; init; } = nameof(Room);
}

internal sealed record AttributeArrayRoom(
    string Id,
    string HotelId,
    string LeaseId,
    DateTime LeasedUntil,
    int MaxGuests,
    IList<RoomSize> Sizes,
    IList<RoomPrice> Prices
)
{
    public string EntityType { get; init; } = nameof(Room);
}

internal sealed record RoomSize(
    string UnitMeasurement,
    int Size
);

internal sealed record RoomPrice(
    string Currency,
    decimal Price
);