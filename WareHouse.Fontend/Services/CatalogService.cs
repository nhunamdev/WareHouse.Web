using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WareHouse.Data;
using WareHouse.Fontend.Models;

namespace WareHouse.Fontend.Services;

public sealed class CatalogService(WareHouseDbContext db, IOptions<ProductImageOptions> imageOptions)
{
    private readonly string _imageBase = imageOptions.Value.RequestPath.TrimEnd('/');

    public async Task<StoreHomeViewModel> GetHomeAsync(string? query, CancellationToken cancellationToken = default)
    {
        var productsQuery = ProductCardsQuery();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            productsQuery = productsQuery.Where(x => x.Name.Contains(term)
                || (x.Category != null && x.Category.Contains(term))
                || (x.ShortDescription != null && x.ShortDescription.Contains(term))
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
                Name = category.Name,
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
                || (x.ShortDescription != null && x.ShortDescription.Contains(term))
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
            Name = category.Name,
            Slug = category.Slug,
            Query = query,
            Products = cards,
            TotalProducts = cards.Count
        };
    }

    private IQueryable<Product> ProductCardsQuery() => db.Products.AsNoTracking()
        .Where(x => x.IsActive)
        .Include(x => x.Items.Where(item => item.IsActive))
        .ThenInclude(item => item.WarehouseStocks)
        .Include(x => x.Items.Where(item => item.IsActive))
        .ThenInclude(item => item.ItemAttributes)
        .ThenInclude(itemAttribute => itemAttribute.AttributeValue)
        .ThenInclude(value => value.Attribute)
        .Include(x => x.Images)
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
            Title = x.Title,
            ImageUrl = ImageUrl(x.ImagePath) ?? string.Empty,
            Url = x.Url
        }).ToList();
    }

    public async Task<ProductDetailViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Include(x => x.Items.Where(item => item.IsActive))
            .ThenInclude(item => item.ItemAttributes)
            .ThenInclude(attribute => attribute.AttributeValue)
            .ThenInclude(value => value.Attribute)
            .Include(x => x.Items.Where(item => item.IsActive))
            .ThenInclude(item => item.WarehouseStocks)
            .Include(x => x.Images.OrderBy(image => image.SortOrder))
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
            .Select(item => new ItemOptionViewModel
            {
                Id = item.Id,
                Label = item.ItemAttributes.Count == 0
                    ? item.Code
                    : string.Join(" · ", item.ItemAttributes
                        .OrderBy(attribute => attribute.AttributeValue.Attribute.Name)
                        .Select(attribute => $"{attribute.AttributeValue.Attribute.Name}: {attribute.AttributeValue.Value}")),
                SalePrice = item.SalePrice,
                Stock = item.WarehouseStocks.Sum(stock => stock.Quantity),
                ImageUrl = ImageUrl(product.Images
                    .Where(image => image.ItemId == item.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.RelativePath)
                    .FirstOrDefault()) ?? images.FirstOrDefault(),
                AttributeValueIds = item.ItemAttributes.Select(x => x.AttributeValueId).OrderBy(x => x).ToList()
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
                Name = group.Key.Name,
                Choices = group
                    .Select(x => x.AttributeValue)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Value)
                    .Select(value => new AttributeChoiceViewModel
                    {
                        Id = value.Id,
                        Name = value.Value,
                        ColorHex = IsColorAttribute(group.Key.Name) ? ColorHex(value.Value) : null
                    })
                    .ToList()
            })
            .ToList();

        var prices = variants.Select(x => x.SalePrice).DefaultIfEmpty(0).ToList();
        return new ProductDetailViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
            DetailContent = product.DetailContent,
            SeoTitle = product.SeoTitle,
            SeoDescription = product.SeoDescription,
            SeoKeywords = product.SeoKeywords,
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
                    .Where(image => image.ItemId == item.Id)
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.RelativePath)
                    .FirstOrDefault();
                return new ProductCardVariantViewModel
                {
                    ItemId = item.Id,
                    ColorValueId = color?.Id,
                    ColorName = color?.Value,
                    ColorHex = color is null ? null : ColorHex(color.Value),
                    SizeValueId = size?.Id,
                    SizeName = size?.Value,
                    ImageUrl = ImageUrl(itemImage ?? cover),
                    Price = item.SalePrice,
                    Stock = item.WarehouseStocks.Sum(stock => stock.Quantity)
                };
            })
            .OrderByDescending(x => x.Stock > 0)
            .ThenBy(x => x.ColorName)
            .ThenBy(x => x.SizeName)
            .ToList();
        return new ProductCardViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Category = product.Category,
            Unit = product.Unit,
            Description = product.Description,
            ShortDescription = product.ShortDescription,
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
}
