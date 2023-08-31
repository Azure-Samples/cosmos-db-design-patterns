namespace DataUploader.Models;

internal sealed record Hotel(
    string Id,
    string HotelId,
    string Name,
    string City,
    IList<Room> Rooms
)
{
    public string EntityType { get; init; } = nameof(Hotel);
}