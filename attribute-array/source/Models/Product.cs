namespace AttributeArray.Models;


internal sealed record AttributePropertyProduct(
    string Id,
    string ProductId,
    string Name,
    string Category,
    string Price,
    int SizeSmall,
    int SizeMedium,
    int SizeLarge,
    string EntityType
);


internal sealed record AttributeArrayProduct(
    string Id,
    string ProductId,
    string Name,
    string Category,
    string Price,
    IList<ProductSize> Sizes,
    string EntityType
);


internal sealed record ProductSize(
    string Size,
    int Count
);

internal sealed record PropertyQueryResult(
    string Name,
    int SizeSmall,
    int SizeMedium,
    int SizeLarge
);

internal sealed record ArrayQueryResult(
    string Name,
    string Size,
    int Count
);