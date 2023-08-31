namespace DataUploader.Models;

internal sealed record Product();

internal sealed record AttributePropertyProduct(
    string Id,
    string ProductId,
    string Name,
    string Category,
    decimal Price,
    int SizeSmall,
    int SizeMedium,
    int SizeLarge
)
{
    public string EntityType { get; init; } = nameof(Product);
}

internal sealed record AttributeArrayProduct(
    string Id,
    string ProductId,
    string Name,
    string Category,
    decimal Price,
    IList<ProductSize> Sizes
)
{
    public string EntityType { get; init; } = nameof(Product);
}

internal sealed record ProductSize(
    string Name,
    int Count
);