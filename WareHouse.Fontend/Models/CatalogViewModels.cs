namespace WareHouse.Fontend.Models;

using WareHouse.Fontend.Services;

public sealed class StoreHomeViewModel
{
    public IReadOnlyList<StoreBannerViewModel> Banners { get; init; } = [];
    public IReadOnlyList<ProductCardViewModel> Products { get; init; } = [];
    public IReadOnlyList<ProductCategorySectionViewModel> CategorySections { get; init; } = [];
    public string? Query { get; init; }
    public int TotalProducts { get; init; }
}

public sealed class StoreBannerViewModel
{
    public string Title { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string? Url { get; init; }
    public bool HasLink => !string.IsNullOrWhiteSpace(Url);
}

public sealed class ProductCategorySectionViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Url => $"/{Slug}";
    public IReadOnlyList<ProductCardViewModel> Products { get; init; } = [];
}

public sealed class StoreCategoryViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Url => $"/{Slug}";
    public string? Query { get; init; }
    public IReadOnlyList<ProductCardViewModel> Products { get; init; } = [];
    public int TotalProducts { get; init; }
}

public sealed class ProductCardViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public decimal TotalStock { get; init; }
    public int VariantCount { get; init; }
    public decimal SoldQuantity { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<ProductCardVariantViewModel> Variants { get; init; } = [];
    public bool IsInStock => TotalStock > 0;
    public string PriceLabel => MinPrice == MaxPrice
        ? MinPrice.ToString("N0")
        : $"{MinPrice:N0} – {MaxPrice:N0}";
    public string ProductUrl => SlugHelper.ProductUrl(Id, Name);
}

public sealed class ProductCardVariantViewModel
{
    public int ItemId { get; init; }
    public int? ColorValueId { get; init; }
    public string? ColorName { get; init; }
    public string? ColorHex { get; init; }
    public int? SizeValueId { get; init; }
    public string? SizeName { get; init; }
    public string? ImageUrl { get; init; }
    public decimal Price { get; init; }
    public decimal Stock { get; init; }
}

public sealed class ProductDetailViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public string? DetailContent { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? SeoKeywords { get; init; }
    public IReadOnlyList<string> Images { get; init; } = [];
    public IReadOnlyList<ItemOptionViewModel> Variants { get; init; } = [];
    public IReadOnlyList<AttributeGroupViewModel> AttributeGroups { get; init; } = [];
    public decimal TotalStock { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public string Slug => SlugHelper.ToSlug(Name);
    public string ProductUrl => SlugHelper.ProductUrl(Id, Name);
    public string PriceLabel => MinPrice == MaxPrice
        ? $"{MinPrice:N0} đ"
        : $"{MinPrice:N0} – {MaxPrice:N0} đ";
}

public sealed class AttributeGroupViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AttributeChoiceViewModel> Choices { get; init; } = [];
}

public sealed class AttributeChoiceViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ColorHex { get; init; }
}

public sealed class ItemOptionViewModel
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal SalePrice { get; init; }
    public decimal Stock { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<int> AttributeValueIds { get; init; } = [];
    public bool IsAvailable => Stock > 0;
}
