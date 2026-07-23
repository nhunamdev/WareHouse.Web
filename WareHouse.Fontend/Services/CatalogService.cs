using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WareHouse.Data;
using WareHouse.Fontend.Models;

namespace WareHouse.Fontend.Services;

public sealed class CatalogService(WareHouseDbContext db, IOptions<ProductImageOptions> imageOptions)
{
    private readonly string _imageBase = imageOptions.Value.RequestPath.TrimEnd('/');
    private static readonly string[] VisibleCategories = StoreCategories.All.Select(x => x.Name).ToArray();

    public async Task<StoreHomeViewModel> GetHomeAsync(string? query, CancellationToken cancellationToken = default)
    {
        var productsQuery = ProductCardsQuery();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            productsQuery = productsQuery.Where(x => x.Name.Contains(term)
                || (x.NameEn != null && x.NameEn.Contains(term))
                || (x.NameDe != null && x.NameDe.Contains(term))
                || (x.Category != null && x.Category.Contains(term))
                || (x.ShortDescription != null && x.ShortDescription.Contains(term))
                || (x.ShortDescriptionEn != null && x.ShortDescriptionEn.Contains(term))
                || (x.ShortDescriptionDe != null && x.ShortDescriptionDe.Contains(term))
                || (x.SeoKeywordsEn != null && x.SeoKeywordsEn.Contains(term))
                || (x.SeoKeywordsDe != null && x.SeoKeywordsDe.Contains(term))
                || (x.SeoKeywords != null && x.SeoKeywords.Contains(term)));
        }

        var products = await productsQuery.ToListAsync(cancellationToken);
        var soldQuantities = await GetSoldQuantitiesAsync(cancellationToken);
        var banners = await GetActiveBannersAsync(cancellationToken);
        var orderedProducts = products
            .OrderByDescending(x => soldQuantities.GetValueOrDefault(x.Id))
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query))
        {
            return new StoreHomeViewModel
            {
                Banners = banners,
                Products = orderedProducts.Select(x => MapCard(x, soldQuantities.GetValueOrDefault(x.Id))).ToList(),
                Query = query,
                TotalProducts = orderedProducts.Count
            };
        }

        var sections = StoreCategories.All
            .Select(category => new ProductCategorySectionViewModel
            {
                Name = category.DisplayName,
                Slug = category.Slug,
                Products = orderedProducts
                    .Where(product => string.Equals(product.Category, category.Name, StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .Select(product => MapCard(product, soldQuantities.GetValueOrDefault(product.Id)))
                    .ToList()
            })
            .Where(section => section.Products.Count > 0)
            .ToList();

        return new StoreHomeViewModel
        {
            Banners = banners,
            Products = sections.SelectMany(x => x.Products).DistinctBy(x => x.Id).ToList(),
            CategorySections = sections,
            TotalProducts = products.Count
        };
    }

    public async Task<StoreCategoryViewModel> GetCategoryAsync(
        StoreCategoryDefinition category,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var productsQuery = ProductCardsQuery().Where(x => x.Category == category.Name);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            productsQuery = productsQuery.Where(x => x.Name.Contains(term)
                || (x.NameEn != null && x.NameEn.Contains(term))
                || (x.NameDe != null && x.NameDe.Contains(term))
                || (x.ShortDescription != null && x.ShortDescription.Contains(term))
                || (x.ShortDescriptionEn != null && x.ShortDescriptionEn.Contains(term))
                || (x.ShortDescriptionDe != null && x.ShortDescriptionDe.Contains(term))
                || (x.SeoKeywordsEn != null && x.SeoKeywordsEn.Contains(term))
                || (x.SeoKeywordsDe != null && x.SeoKeywordsDe.Contains(term))
                || (x.SeoKeywords != null && x.SeoKeywords.Contains(term)));
        }

        var products = await productsQuery.ToListAsync(cancellationToken);
        var soldQuantities = await GetSoldQuantitiesAsync(cancellationToken);
        var cards = products
            .OrderByDescending(x => soldQuantities.GetValueOrDefault(x.Id))
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => MapCard(x, soldQuantities.GetValueOrDefault(x.Id)))
            .ToList();

        return new StoreCategoryViewModel
        {
            Name = category.DisplayName,
            Slug = category.Slug,
            Query = query,
            Products = cards,
            TotalProducts = cards.Count
        };
    }

    public async Task<StoreSearchViewModel> SearchAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var term = query?.Trim();
        if (term?.Length > 100) term = term[..100];
        if (string.IsNullOrWhiteSpace(term))
            return new StoreSearchViewModel();

        var products = await ProductCardsQuery()
            .Where(x => x.Name.Contains(term)
                || (x.NameEn != null && x.NameEn.Contains(term))
                || (x.NameDe != null && x.NameDe.Contains(term))
                || (x.Category != null && x.Category.Contains(term))
                || (x.ShortDescription != null && x.ShortDescription.Contains(term))
                || (x.ShortDescriptionEn != null && x.ShortDescriptionEn.Contains(term))
                || (x.ShortDescriptionDe != null && x.ShortDescriptionDe.Contains(term))
                || (x.SeoKeywordsEn != null && x.SeoKeywordsEn.Contains(term))
                || (x.SeoKeywordsDe != null && x.SeoKeywordsDe.Contains(term))
                || (x.SeoKeywords != null && x.SeoKeywords.Contains(term)))
            .ToListAsync(cancellationToken);
        var soldQuantities = await GetSoldQuantitiesAsync(cancellationToken);
        var cards = products
            .OrderByDescending(x => x.Name.StartsWith(term))
            .ThenByDescending(x => soldQuantities.GetValueOrDefault(x.Id))
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => MapCard(x, soldQuantities.GetValueOrDefault(x.Id)))
            .ToList();

        return new StoreSearchViewModel
        {
            Query = term,
            Products = cards
        };
    }

    public async Task<IReadOnlyList<ProductSearchSuggestionViewModel>> GetSearchSuggestionsAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var term = query?.Trim();
        if (term?.Length > 100) term = term[..100];
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return [];

        var suggestions = await db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Category != null && VisibleCategories.Contains(x.Category) &&
                (x.Name.Contains(term) ||
                 (x.NameEn != null && x.NameEn.Contains(term)) ||
                 (x.NameDe != null && x.NameDe.Contains(term))))
            .OrderByDescending(x => x.Name.StartsWith(term))
            .ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.NameEn,
                x.NameDe,
                x.Category,
                ImagePath = x.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.RelativePath)
                    .FirstOrDefault()
            })
            .Take(6)
            .ToListAsync(cancellationToken);

        return suggestions.Select(x =>
        {
            var name = Localized(x.Name, x.NameEn, x.NameDe);
            return new ProductSearchSuggestionViewModel
            {
                Name = name,
                Category = StoreCulture.CategoryName(x.Category),
                ImageUrl = ImageUrl(x.ImagePath),
                ProductUrl = StoreCulture.Path(SlugHelper.ProductUrl(x.Id, name))
            };
        }).ToList();
    }

    private IQueryable<Product> ProductCardsQuery() => db.Products.AsNoTracking()
        .Where(x => x.IsActive && x.Category != null && VisibleCategories.Contains(x.Category))
        .Include(x => x.Items.Where(item => item.IsActive))
        .ThenInclude(item => item.WarehouseStocks)
        .Include(x => x.Items.Where(item => item.IsActive))
        .ThenInclude(item => item.ItemAttributes)
        .ThenInclude(itemAttribute => itemAttribute.AttributeValue)
        .ThenInclude(value => value.Attribute)
        .Include(x => x.Images)
        .ThenInclude(image => image.ItemAssignments)
        .AsSplitQuery();

    private Task<Dictionary<int, decimal>> GetSoldQuantitiesAsync(CancellationToken cancellationToken) =>
        db.StockDocumentDetails.AsNoTracking()
            .Where(x => x.Document.DocumentType == StockDocumentType.Sale
                && x.Document.Status == DocumentStatus.Completed)
            .GroupBy(x => x.Item.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

    private async Task<List<StoreBannerViewModel>> GetActiveBannersAsync(CancellationToken cancellationToken)
    {
        var banners = await db.StoreBanners.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return banners.Select(x => new StoreBannerViewModel
        {
            Title = Localized(x.Title, x.TitleEn, x.TitleDe),
            ImageUrl = ImageUrl(x.ImagePath) ?? string.Empty,
            Url = x.Url
        }).ToList();
    }

    public async Task<ProductDetailViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive && x.Category != null && VisibleCategories.Contains(x.Category))
            .Include(x => x.Items.Where(item => item.IsActive))
            .ThenInclude(item => item.ItemAttributes)
            .ThenInclude(attribute => attribute.AttributeValue)
            .ThenInclude(value => value.Attribute)
            .Include(x => x.Items.Where(item => item.IsActive))
            .ThenInclude(item => item.WarehouseStocks)
            .Include(x => x.Images.OrderBy(image => image.SortOrder))
            .ThenInclude(image => image.ItemAssignments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null) return null;

        var images = product.Images
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .Select(x => ImageUrl(x.RelativePath))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var variants = product.Items
            .OrderBy(x => x.SalePrice)
            .ThenBy(x => x.Code)
            .Select(item =>
            {
                var colorValueId = item.ItemAttributes
                    .Where(attribute => IsColorAttribute(attribute.AttributeValue.Attribute.Name))
                    .Select(attribute => (int?)attribute.AttributeValueId)
                    .FirstOrDefault();
                var itemImage = product.Images
                    .Where(image =>
                        image.ItemAssignments.Any(assignment => assignment.ItemId == item.Id) ||
                        (colorValueId.HasValue && image.ColorValueId == colorValueId) ||
                        (!image.ColorValueId.HasValue && image.ItemId == item.Id))
                    .OrderBy(image => image.ItemAssignments.Any(assignment => assignment.ItemId == item.Id) ? 0 : 1)
                    .ThenByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.RelativePath)
                    .FirstOrDefault();
                return new ItemOptionViewModel
                {
                    Id = item.Id,
                    Label = item.ItemAttributes.Count == 0
                        ? item.Code
                        : string.Join(" · ", item.ItemAttributes
                            .OrderBy(attribute => attribute.AttributeValue.Attribute.Name)
                            .Select(attribute => $"{StoreCulture.AttributeName(attribute.AttributeValue.Attribute.Name)}: {StoreCulture.AttributeValue(attribute.AttributeValue.Value)}")),
                    SalePrice = item.SalePrice,
                    Stock = item.WarehouseStocks.Sum(stock => stock.Quantity),
                    ImageUrl = ImageUrl(itemImage) ?? images.FirstOrDefault(),
                    AttributeValueIds = item.ItemAttributes.Select(x => x.AttributeValueId).OrderBy(x => x).ToList()
                };
            })
            .ToList();

        var attributeGroups = product.Items
            .SelectMany(x => x.ItemAttributes)
            .GroupBy(x => new { x.AttributeValue.Attribute.Id, x.AttributeValue.Attribute.Name })
            .OrderBy(x => IsColorAttribute(x.Key.Name) ? 0 : 1)
            .ThenBy(x => x.Key.Name)
            .Select(group => new AttributeGroupViewModel
            {
                Id = group.Key.Id,
                Name = StoreCulture.AttributeName(group.Key.Name),
                Choices = group
                    .Select(x => x.AttributeValue)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Value)
                    .Select(value => new AttributeChoiceViewModel
                    {
                        Id = value.Id,
                        Name = StoreCulture.AttributeValue(value.Value),
                        ColorHex = IsColorAttribute(group.Key.Name) ? ColorHex(value.Value) : null
                    })
                    .ToList()
            })
            .ToList();

        var prices = variants.Select(x => x.SalePrice).DefaultIfEmpty(0).ToList();
        var localizedName = Localized(product.Name, product.NameEn, product.NameDe);
        var localizedShortDescription = LocalizedNullable(product.ShortDescription, product.ShortDescriptionEn, product.ShortDescriptionDe);
        return new ProductDetailViewModel
        {
            Id = product.Id,
            Name = localizedName,
            Category = StoreCulture.CategoryName(product.Category),
            Unit = StoreCulture.UnitName(product.Unit),
            Description = localizedShortDescription ?? product.Description,
            ShortDescription = localizedShortDescription,
            DetailContent = LocalizedNullable(product.DetailContent, product.DetailContentEn, product.DetailContentDe),
            SeoTitle = LocalizedNullable(product.SeoTitle, product.SeoTitleEn, product.SeoTitleDe) ?? localizedName,
            SeoDescription = LocalizedNullable(product.SeoDescription, product.SeoDescriptionEn, product.SeoDescriptionDe) ?? localizedShortDescription,
            SeoKeywords = LocalizedNullable(product.SeoKeywords, product.SeoKeywordsEn, product.SeoKeywordsDe),
            Images = images,
            Variants = variants,
            AttributeGroups = attributeGroups,
            TotalStock = variants.Sum(x => x.Stock),
            MinPrice = prices.Min(),
            MaxPrice = prices.Max()
        };
    }

    private ProductCardViewModel MapCard(Product product, decimal soldQuantity = 0)
    {
        var variants = product.Items.Where(x => x.IsActive).ToList();
        var prices = variants.Select(x => x.SalePrice).DefaultIfEmpty(0).ToList();
        var cover = product.Images
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .Select(x => x.RelativePath)
            .FirstOrDefault();
        var variantOptions = variants
            .Select(item =>
            {
                var color = item.ItemAttributes
                    .Select(attribute => attribute.AttributeValue)
                    .FirstOrDefault(value => IsColorAttribute(value.Attribute.Name));
                var size = item.ItemAttributes
                    .Select(attribute => attribute.AttributeValue)
                    .FirstOrDefault(value => IsSizeAttribute(value.Attribute.Name));
                var itemImage = product.Images
                    .Where(image =>
                        image.ItemAssignments.Any(assignment => assignment.ItemId == item.Id) ||
                        (color != null && image.ColorValueId == color.Id) ||
                        (!image.ColorValueId.HasValue && image.ItemId == item.Id))
                    .OrderBy(image => image.ItemAssignments.Any(assignment => assignment.ItemId == item.Id) ? 0 : 1)
                    .ThenByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.RelativePath)
                    .FirstOrDefault();
                return new ProductCardVariantViewModel
                {
                    ItemId = item.Id,
                    ColorValueId = color?.Id,
                    ColorName = color is null ? null : StoreCulture.AttributeValue(color.Value),
                    ColorHex = color is null ? null : ColorHex(color.Value),
                    SizeValueId = size?.Id,
                    SizeName = size is null ? null : StoreCulture.AttributeValue(size.Value),
                    ImageUrl = ImageUrl(itemImage ?? cover),
                    Price = item.SalePrice,
                    Stock = item.WarehouseStocks.Sum(stock => stock.Quantity)
                };
            })
            .OrderByDescending(x => x.Stock > 0)
            .ThenBy(x => x.ColorName)
            .ThenBy(x => x.SizeName)
            .ToList();
        var localizedName = Localized(product.Name, product.NameEn, product.NameDe);
        var localizedShortDescription = LocalizedNullable(product.ShortDescription, product.ShortDescriptionEn, product.ShortDescriptionDe);
        return new ProductCardViewModel
        {
            Id = product.Id,
            Name = localizedName,
            Category = StoreCulture.CategoryName(product.Category),
            Unit = StoreCulture.UnitName(product.Unit),
            Description = localizedShortDescription ?? product.Description,
            ShortDescription = localizedShortDescription,
            MinPrice = prices.Min(),
            MaxPrice = prices.Max(),
            TotalStock = variants.SelectMany(x => x.WarehouseStocks).Sum(x => x.Quantity),
            VariantCount = variants.Count,
            SoldQuantity = soldQuantity,
            ImageUrl = ImageUrl(cover) ?? variantOptions.Select(x => x.ImageUrl).FirstOrDefault(x => x is not null),
            Variants = variantOptions
        };
    }

    private static bool IsColorAttribute(string name) =>
        name.Contains("màu", StringComparison.OrdinalIgnoreCase)
        || name.Contains("mau", StringComparison.OrdinalIgnoreCase)
        || name.Contains("color", StringComparison.OrdinalIgnoreCase);

    private static bool IsSizeAttribute(string name) =>
        name.Contains("size", StringComparison.OrdinalIgnoreCase)
        || name.Contains("kích cỡ", StringComparison.OrdinalIgnoreCase)
        || name.Contains("kich co", StringComparison.OrdinalIgnoreCase);

    private static string ColorHex(string value)
    {
        var color = value.Trim().ToLowerInvariant();
        if (color.Contains("trắng") || color.Contains("white")) return "#f8fafc";
        if (color.Contains("đen") || color.Contains("black")) return "#20252b";
        if (color.Contains("đỏ") || color.Contains("red")) return "#df4747";
        if (color.Contains("xanh lá") || color.Contains("green")) return "#438a68";
        if (color.Contains("xanh") || color.Contains("blue")) return "#4779bd";
        if (color.Contains("vàng") || color.Contains("yellow")) return "#e6bf45";
        if (color.Contains("hồng") || color.Contains("pink")) return "#e99aaa";
        if (color.Contains("tím") || color.Contains("purple")) return "#8b6bb1";
        if (color.Contains("cam") || color.Contains("orange")) return "#e8843c";
        if (color.Contains("xám") || color.Contains("ghi") || color.Contains("gray")) return "#929aa3";
        if (color.Contains("bạc") || color.Contains("silver")) return "#c1c7ce";
        if (color.Contains("nâu") || color.Contains("brown")) return "#8d674d";
        if (color.Contains("kem") || color.Contains("be")) return "#eadfc7";
        return "#c8d0d7";
    }

    private string? ImageUrl(string? relativePath) => string.IsNullOrWhiteSpace(relativePath)
        ? null
        : $"{_imageBase}/{string.Join('/', relativePath.Split('/', '\\').Select(Uri.EscapeDataString))}";

    private static string Localized(string vietnamese, string? english, string? german) =>
        LocalizedNullable(vietnamese, english, german) ?? vietnamese;

    private static string? LocalizedNullable(string? vietnamese, string? english, string? german)
    {
        var selected = StoreCulture.CurrentCode switch
        {
            "en" => english,
            "de" => german,
            _ => vietnamese
        };
        return string.IsNullOrWhiteSpace(selected) ? vietnamese : selected;
    }
}
