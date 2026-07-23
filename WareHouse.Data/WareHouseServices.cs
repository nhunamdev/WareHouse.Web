using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WareHouse.Data;

public class WareHouseServices(
    WareHouseDbContext db,
    ILogger<WareHouseServices> logger,
    IAuditUserProvider? auditUserProvider = null)
{
    private readonly WareHouseDbContext _db = db;
    private readonly ILogger<WareHouseServices> _logger = logger;
    private readonly IAuditUserProvider? _auditUserProvider = auditUserProvider;
    private const int DefaultPageSize = 20;

    #region Products

    public async Task<PagedResult<Product>> GetProductsAsync(
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Products.AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Items).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.Items).ThenInclude(x => x.WarehouseStocks)
            .AsSplitQuery()
            .AsQueryable();
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) ||
                                     (x.Category != null && x.Category.Contains(keyword)) ||
                                     (x.ShortDescription != null && x.ShortDescription.Contains(keyword)) ||
                                     (x.SeoTitle != null && x.SeoTitle.Contains(keyword)) ||
                                     (x.SeoKeywords != null && x.SeoKeywords.Contains(keyword)) ||
                                     x.Items.Any(item => item.ItemAttributes.Any(attribute =>
                                         attribute.AttributeValue.Value.Contains(keyword))));
        }

        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            ("category", true) => query.OrderByDescending(x => x.Category),
            ("category", false) => query.OrderBy(x => x.Category),
            ("unit", true) => query.OrderByDescending(x => x.Unit),
            ("unit", false) => query.OrderBy(x => x.Unit),
            ("variants", true) => query.OrderByDescending(x => x.Items.Count),
            ("variants", false) => query.OrderBy(x => x.Items.Count),
            ("stock", true) => query.OrderByDescending(x => x.Items.SelectMany(i => i.WarehouseStocks).Sum(s => s.Quantity)),
            ("stock", false) => query.OrderBy(x => x.Items.SelectMany(i => i.WarehouseStocks).Sum(s => s.Quantity)),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            ("status", false) => query.OrderBy(x => x.IsActive),
            _ => query.OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Items.Any(item =>
                    item.WarehouseStocks.Any(stock => stock.Quantity > 0)))
                .ThenByDescending(x => x.Id)
                .ThenBy(x => x.Name)
        };
        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<Product?> GetProductAsync(int id) =>
        _db.Products.AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.Items).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<Product>> GetActiveProductsAsync() =>
        _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Items.Any(item =>
                item.WarehouseStocks.Any(stock => stock.Quantity > 0)))
            .ThenByDescending(x => x.Id)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public Task<List<string>> GetProductCategoriesAsync() =>
        _db.Products.AsNoTracking()
            .Where(x => x.Category != null && x.Category != "")
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    public Task<List<string>> GetProductUnitsAsync() =>
        _db.Products.AsNoTracking()
            .Where(x => x.Unit != null && x.Unit != "")
            .Select(x => x.Unit!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    public async Task<(int ProductCount, int ActiveItemCount)> GetProductCatalogSummaryAsync()
    {
        var productCount = await _db.Products.AsNoTracking().CountAsync();
        var activeItemCount = await _db.Items.AsNoTracking()
            .CountAsync(x => x.IsActive && x.Product.IsActive);
        return (productCount, activeItemCount);
    }

    public async Task<ServiceResult> SaveProductAsync(Product model)
    {
        model.Code = NormalizeCode(model.Code);
        model.Name = Clean(model.Name);
        model.Category = CleanNullable(model.Category);
        model.Unit = CleanNullable(model.Unit);
        model.ShortDescription = CleanNullable(model.ShortDescription);
        model.DetailContent = CleanNullable(model.DetailContent);
        NormalizeProductContent(model);
        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult.Fail("Tên sản phẩm là bắt buộc.");

        try
        {
            if (model.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(model.Code))
                    model.Code = await GenerateProductCodeAsync();
                if (await _db.Products.AnyAsync(x => x.Code == model.Code))
                    return ServiceResult.Fail("Mã sản phẩm đã tồn tại.");

                model.IsActive = true;
                _db.Products.Add(model);
            }
            else
            {
                var entity = await _db.Products.FindAsync(model.Id);
                if (entity is null) return ServiceResult.Fail("Không tìm thấy sản phẩm.");

                if (string.IsNullOrWhiteSpace(model.Code))
                    model.Code = entity.Code;
                if (await _db.Products.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
                    return ServiceResult.Fail("Mã sản phẩm đã tồn tại.");

                entity.Code = model.Code;
                entity.Name = model.Name;
                entity.NameEn = model.NameEn;
                entity.NameDe = model.NameDe;
                entity.Category = model.Category;
                entity.Unit = model.Unit;
                entity.Description = model.Description;
                entity.ShortDescription = model.ShortDescription;
                entity.ShortDescriptionEn = model.ShortDescriptionEn;
                entity.ShortDescriptionDe = model.ShortDescriptionDe;
                entity.DetailContent = model.DetailContent;
                entity.DetailContentEn = model.DetailContentEn;
                entity.DetailContentDe = model.DetailContentDe;
                entity.SeoTitle = model.SeoTitle;
                entity.SeoTitleEn = model.SeoTitleEn;
                entity.SeoTitleDe = model.SeoTitleDe;
                entity.SeoDescription = model.SeoDescription;
                entity.SeoDescriptionEn = model.SeoDescriptionEn;
                entity.SeoDescriptionDe = model.SeoDescriptionDe;
                entity.SeoKeywords = model.SeoKeywords;
                entity.SeoKeywordsEn = model.SeoKeywordsEn;
                entity.SeoKeywordsDe = model.SeoKeywordsDe;
                entity.IsActive = model.IsActive;
            }

            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã lưu sản phẩm.", model.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể lưu sản phẩm {ProductId}", model.Id);
            return ServiceResult.Fail("Không thể lưu sản phẩm. Vui lòng kiểm tra dữ liệu trùng.");
        }
    }

    public async Task<ServiceResult> SaveProductWithItemsAsync(
        Product model,
        IReadOnlyList<ProductItemInput>? itemInputs)
    {
        model.Code = NormalizeCode(model.Code);
        model.Name = Clean(model.Name);
        model.Category = CleanNullable(model.Category);
        model.Unit = CleanNullable(model.Unit);
        model.ShortDescription = CleanNullable(model.ShortDescription);
        model.DetailContent = CleanNullable(model.DetailContent);
        NormalizeProductContent(model);
        itemInputs ??= [];

        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult.Fail("Tên sản phẩm là bắt buộc.");
        if (itemInputs.Count == 0)
            return ServiceResult.Fail("Sản phẩm phải có ít nhất một phân loại.");
        if (itemInputs.Count > 200)
            return ServiceResult.Fail("Mỗi sản phẩm chỉ được lưu tối đa 200 phân loại.");

        var normalizedItems = itemInputs.Select((item, index) => new
        {
            RowNumber = index + 1,
            Source = item,
            item.Id,
            item.CostPrice,
            item.SalePrice,
            AttributeValueIds = item.AttributeValueIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList()
        }).ToList();

        var invalidPriceRow = normalizedItems.FirstOrDefault(x => x.CostPrice < 0 || x.SalePrice < 0);
        if (invalidPriceRow is not null)
            return ServiceResult.Fail($"Giá tại dòng phân loại {invalidPriceRow.RowNumber} không được âm.");
        if (normalizedItems.Any(x => x.Id < 0))
            return ServiceResult.Fail("Dữ liệu phân loại không hợp lệ.");
        if (normalizedItems.Where(x => x.Id > 0).GroupBy(x => x.Id).Any(x => x.Count() > 1))
            return ServiceResult.Fail("Danh sách đang chứa phân loại bị lặp.");
        if (model.Id == 0 && normalizedItems.Any(x => x.Id > 0))
            return ServiceResult.Fail("Dữ liệu phân loại không thuộc sản phẩm mới.");

        Product? entity = null;
        if (model.Id > 0)
        {
            entity = await _db.Products
                .Include(x => x.Items).ThenInclude(x => x.ItemAttributes)
                .Include(x => x.Items).ThenInclude(x => x.WarehouseStocks)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == model.Id);
            if (entity is null) return ServiceResult.Fail("Không tìm thấy sản phẩm.");

            var existingIds = entity.Items.Select(x => x.Id).ToHashSet();
            if (normalizedItems.Any(x => x.Id > 0 && !existingIds.Contains(x.Id)))
                return ServiceResult.Fail("Có phân loại không thuộc sản phẩm này.");
        }

        if (string.IsNullOrWhiteSpace(model.Code))
            model.Code = entity?.Code ?? await GenerateProductCodeAsync();
        if (await _db.Products.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
            return ServiceResult.Fail("Mã sản phẩm đã tồn tại.");

        var selectedValueIds = normalizedItems
            .SelectMany(x => x.AttributeValueIds)
            .Distinct()
            .ToList();
        var values = await _db.AttributeValues.AsNoTracking()
            .Where(x => selectedValueIds.Contains(x.Id) && x.IsActive && x.Attribute.IsActive)
            .Select(x => new { x.Id, x.AttributeId })
            .ToListAsync();
        if (values.Count != selectedValueIds.Count)
            return ServiceResult.Fail("Có giá trị thuộc tính không hợp lệ hoặc đã bị vô hiệu hóa.");

        var attributeByValue = values.ToDictionary(x => x.Id, x => x.AttributeId);
        foreach (var item in normalizedItems)
        {
            if (item.AttributeValueIds
                .Select(x => attributeByValue[x])
                .GroupBy(x => x)
                .Any(x => x.Count() > 1))
            {
                return ServiceResult.Fail(
                    $"Dòng phân loại {item.RowNumber} chỉ được chọn một giá trị trong mỗi nhóm thuộc tính.");
            }
        }

        var duplicateCombination = normalizedItems
            .GroupBy(x => string.Join(",", x.AttributeValueIds))
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateCombination is not null)
        {
            var duplicatedRows = string.Join(", ", duplicateCombination.Select(x => x.RowNumber));
            return ServiceResult.Fail($"Các dòng {duplicatedRows} đang trùng tổ hợp thuộc tính.");
        }

        var submittedIds = normalizedItems.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
        var removedItems = entity?.Items.Where(x => !submittedIds.Contains(x.Id)).ToList() ?? [];
        var removedIds = removedItems.Select(x => x.Id).ToList();
        if (removedItems.Any(x => x.WarehouseStocks.Any(stock => stock.Quantity != 0)))
            return ServiceResult.Fail("Không thể xóa phân loại vẫn còn tồn kho.");
        if (removedIds.Count > 0 &&
            await _db.StockDocumentDetails.AnyAsync(x => removedIds.Contains(x.ItemId)))
            return ServiceResult.Fail("Không thể xóa phân loại đã phát sinh giao dịch.");

        try
        {
            if (entity is null)
            {
                entity = new Product
                {
                    Code = model.Code,
                    Name = model.Name,
                    NameEn = model.NameEn,
                    NameDe = model.NameDe,
                    Category = model.Category,
                    Unit = model.Unit,
                    Description = model.Description,
                    ShortDescription = model.ShortDescription,
                    ShortDescriptionEn = model.ShortDescriptionEn,
                    ShortDescriptionDe = model.ShortDescriptionDe,
                    DetailContent = model.DetailContent,
                    DetailContentEn = model.DetailContentEn,
                    DetailContentDe = model.DetailContentDe,
                    SeoTitle = model.SeoTitle,
                    SeoTitleEn = model.SeoTitleEn,
                    SeoTitleDe = model.SeoTitleDe,
                    SeoDescription = model.SeoDescription,
                    SeoDescriptionEn = model.SeoDescriptionEn,
                    SeoDescriptionDe = model.SeoDescriptionDe,
                    SeoKeywords = model.SeoKeywords,
                    SeoKeywordsEn = model.SeoKeywordsEn,
                    SeoKeywordsDe = model.SeoKeywordsDe,
                    IsActive = true
                };
                _db.Products.Add(entity);
            }
            else
            {
                entity.Code = model.Code;
                entity.Name = model.Name;
                entity.NameEn = model.NameEn;
                entity.NameDe = model.NameDe;
                entity.Category = model.Category;
                entity.Unit = model.Unit;
                entity.Description = model.Description;
                entity.ShortDescription = model.ShortDescription;
                entity.ShortDescriptionEn = model.ShortDescriptionEn;
                entity.ShortDescriptionDe = model.ShortDescriptionDe;
                entity.DetailContent = model.DetailContent;
                entity.DetailContentEn = model.DetailContentEn;
                entity.DetailContentDe = model.DetailContentDe;
                entity.SeoTitle = model.SeoTitle;
                entity.SeoTitleEn = model.SeoTitleEn;
                entity.SeoTitleDe = model.SeoTitleDe;
                entity.SeoDescription = model.SeoDescription;
                entity.SeoDescriptionEn = model.SeoDescriptionEn;
                entity.SeoDescriptionDe = model.SeoDescriptionDe;
                entity.SeoKeywords = model.SeoKeywords;
                entity.SeoKeywordsEn = model.SeoKeywordsEn;
                entity.SeoKeywordsDe = model.SeoKeywordsDe;
                entity.IsActive = model.IsActive;
            }

            foreach (var removedItem in removedItems)
            {
                _db.WarehouseStocks.RemoveRange(removedItem.WarehouseStocks);
                _db.Items.Remove(removedItem);
            }

            var existingById = entity.Items.ToDictionary(x => x.Id);
            var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var createdItems = new List<(ProductItemInput Input, Item Entity)>();
            foreach (var input in normalizedItems)
            {
                if (input.Id > 0)
                {
                    var item = existingById[input.Id];
                    item.CostPrice = input.CostPrice;
                    item.SalePrice = input.SalePrice;

                    var requestedValueIds = input.AttributeValueIds.ToHashSet();
                    var removedAttributes = item.ItemAttributes
                        .Where(x => !requestedValueIds.Contains(x.AttributeValueId))
                        .ToList();
                    _db.ItemAttributes.RemoveRange(removedAttributes);
                    foreach (var valueId in requestedValueIds.Except(
                                 item.ItemAttributes.Select(x => x.AttributeValueId)))
                    {
                        item.ItemAttributes.Add(new ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeValueId = valueId
                        });
                    }
                    continue;
                }

                string itemCode;
                do
                {
                    itemCode = await GenerateItemCodeAsync();
                } while (!reservedCodes.Add(itemCode));

                var newItem = new Item
                {
                    Code = itemCode,
                    CostPrice = input.CostPrice,
                    SalePrice = input.SalePrice,
                    IsActive = true,
                    ItemAttributes = input.AttributeValueIds
                        .Select(x => new ItemAttribute { AttributeValueId = x })
                        .ToList()
                };
                entity.Items.Add(newItem);
                createdItems.Add((input.Source, newItem));
            }

            // Một lần lưu phân loại cũng là một lần cập nhật sản phẩm.
            // Đánh dấu audit để Product được ghi người/ngày sửa ngay cả khi
            // người dùng chỉ thay giá hoặc thuộc tính của các phân loại.
            if (model.Id > 0)
                _db.Entry(entity).Property(x => x.UpdatedAt).IsModified = true;

            await _db.SaveChangesAsync();

            // Trả ID vừa sinh về đúng dòng nhập để controller có thể quy đổi
            // khóa giao diện sang Item thật khi lưu gán ảnh nhiều phân loại.
            foreach (var created in createdItems)
                created.Input.Id = created.Entity.Id;

            var message = model.Id == 0
                ? $"Đã lưu sản phẩm và {normalizedItems.Count} phân loại."
                : $"Đã cập nhật sản phẩm và {normalizedItems.Count} phân loại.";
            return ServiceResult.Ok(message, entity.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể lưu sản phẩm và danh sách phân loại {ProductCode}", model.Code);
            return ServiceResult.Fail("Không thể lưu sản phẩm và danh sách phân loại. Vui lòng kiểm tra dữ liệu trùng.");
        }
    }

    public async Task<ServiceResult> DeleteProductAsync(int id)
    {
        var entity = await _db.Products
            .Include(x => x.Items).ThenInclude(x => x.ItemAttributes)
            .Include(x => x.Items).ThenInclude(x => x.WarehouseStocks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy sản phẩm.");

        var itemIds = entity.Items.Select(x => x.Id).ToList();
        if (entity.Items.Any(x => x.WarehouseStocks.Any(stock => stock.Quantity != 0)))
            return ServiceResult.Fail("Không thể xóa sản phẩm vì vẫn còn phân loại có tồn kho.");
        if (itemIds.Count > 0 &&
            await _db.StockDocumentDetails.AnyAsync(x => itemIds.Contains(x.ItemId)))
            return ServiceResult.Fail("Không thể xóa sản phẩm vì phân loại đã phát sinh giao dịch.");

        try
        {
            _db.WarehouseStocks.RemoveRange(entity.Items.SelectMany(x => x.WarehouseStocks));
            _db.Items.RemoveRange(entity.Items);
            _db.Products.Remove(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa sản phẩm.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa sản phẩm {ProductId}", id);
            return ServiceResult.Fail("Không thể xóa sản phẩm vì đang được dữ liệu khác sử dụng.");
        }
    }

    #endregion

    #region Attributes

    public async Task<PagedResult<ProductAttribute>> GetAttributesAsync(
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Attributes.AsNoTracking().Include(x => x.Values).AsQueryable();
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Name.Contains(keyword));
        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("values", true) => query.OrderByDescending(x => x.Values.Count),
            ("values", false) => query.OrderBy(x => x.Values.Count),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            ("status", false) => query.OrderBy(x => x.IsActive),
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<ProductAttribute?> GetAttributeAsync(int id) =>
        _db.Attributes.AsNoTracking().Include(x => x.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Value))
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ProductAttribute?> GetAttributeWithSortedValuesAsync(
        int id, string? sort, string? direction)
    {
        var attribute = await _db.Attributes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (attribute is null) return null;

        var query = _db.AttributeValues.AsNoTracking().Where(x => x.AttributeId == id);
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("value", true) => query.OrderByDescending(x => x.Value),
            ("order", true) => query.OrderByDescending(x => x.SortOrder),
            ("order", false) => query.OrderBy(x => x.SortOrder),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            ("status", false) => query.OrderBy(x => x.IsActive),
            ("value", false) => query.OrderBy(x => x.Value),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        attribute.Values = await ordered.ThenByDescending(x => x.Id).ToListAsync();
        return attribute;
    }

    public Task<List<ProductAttribute>> GetActiveAttributesAsync() =>
        _db.Attributes.AsNoTracking().Where(x => x.IsActive)
            .Include(x => x.Values.Where(v => v.IsActive).OrderBy(v => v.SortOrder).ThenBy(v => v.Value))
            .OrderBy(x => x.Name).ToListAsync();

    public async Task<ServiceResult> SaveAttributeAsync(ProductAttribute model)
    {
        model.Name = Clean(model.Name);
        if (string.IsNullOrWhiteSpace(model.Name)) return ServiceResult.Fail("Tên thuộc tính là bắt buộc.");
        if (await _db.Attributes.AnyAsync(x => x.Name == model.Name && x.Id != model.Id))
            return ServiceResult.Fail("Tên thuộc tính đã tồn tại.");

        if (model.Id == 0)
        {
            model.IsActive = true;
            _db.Attributes.Add(model);
        }
        else
        {
            var entity = await _db.Attributes.FindAsync(model.Id);
            if (entity is null) return ServiceResult.Fail("Không tìm thấy thuộc tính.");
            entity.Name = model.Name;
            entity.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã lưu thuộc tính.", model.Id);
    }

    public async Task<ServiceResult> DeleteAttributeAsync(int id)
    {
        var entity = await _db.Attributes.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy thuộc tính.");
        var valueIds = entity.Values.Select(x => x.Id).ToList();
        if (valueIds.Count != 0 &&
            await _db.ItemAttributes.AnyAsync(x => valueIds.Contains(x.AttributeValueId)))
            return ServiceResult.Fail("Thuộc tính đang được phân loại sản phẩm sử dụng nên không thể xóa.");

        try
        {
            _db.AttributeValues.RemoveRange(entity.Values);
            _db.Attributes.Remove(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa thuộc tính và các giá trị.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa thuộc tính {AttributeId}", id);
            return ServiceResult.Fail("Không thể xóa thuộc tính vì đang được dữ liệu khác sử dụng.");
        }
    }

    public Task<List<AttributeValue>> GetAttributeValuesAsync(int attributeId) =>
        _db.AttributeValues.AsNoTracking().Where(x => x.AttributeId == attributeId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Value).ToListAsync();

    public Task<AttributeValue?> GetAttributeValueAsync(int id) =>
        _db.AttributeValues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult> SaveAttributeValueAsync(AttributeValue model)
    {
        model.Value = Clean(model.Value);
        if (!await _db.Attributes.AnyAsync(x => x.Id == model.AttributeId))
            return ServiceResult.Fail("Thuộc tính không tồn tại.");
        if (string.IsNullOrWhiteSpace(model.Value)) return ServiceResult.Fail("Giá trị là bắt buộc.");
        if (await _db.AttributeValues.AnyAsync(x => x.AttributeId == model.AttributeId &&
                                                   x.Value == model.Value && x.Id != model.Id))
            return ServiceResult.Fail("Giá trị này đã tồn tại trong thuộc tính.");

        if (model.Id == 0)
        {
            model.IsActive = true;
            _db.AttributeValues.Add(model);
        }
        else
        {
            var entity = await _db.AttributeValues.FindAsync(model.Id);
            if (entity is null) return ServiceResult.Fail("Không tìm thấy giá trị thuộc tính.");
            entity.Value = model.Value;
            entity.SortOrder = model.SortOrder;
            entity.IsActive = model.IsActive;
        }

        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã lưu giá trị thuộc tính.", model.Id);
    }

    public async Task<ServiceResult> DeleteAttributeValueAsync(int id)
    {
        var entity = await _db.AttributeValues.FindAsync(id);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy giá trị thuộc tính.");
        if (await _db.ItemAttributes.AnyAsync(x => x.AttributeValueId == id))
            return ServiceResult.Fail("Giá trị thuộc tính đang được phân loại sản phẩm sử dụng nên không thể xóa.");

        try
        {
            _db.AttributeValues.Remove(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa giá trị thuộc tính.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa giá trị thuộc tính {AttributeValueId}", id);
            return ServiceResult.Fail("Không thể xóa giá trị thuộc tính vì đang được dữ liệu khác sử dụng.");
        }
    }

    #endregion

    #region Items

    public async Task<PagedResult<ItemRow>> GetItemsAsync(
        int? productId, string? keyword, int page = 1, int pageSize = DefaultPageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Items.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.WarehouseStocks).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Code.Contains(keyword) || x.Product.Name.Contains(keyword) ||
                                     (x.Barcode != null && x.Barcode.Contains(keyword)));

        var total = await query.CountAsync();
        var entities = await query
            .OrderByDescending(x => x.Product.CreatedAt)
            .ThenByDescending(x => x.WarehouseStocks.Any(stock => stock.Quantity > 0))
            .ThenByDescending(x => x.ProductId)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ThenBy(x => x.Code)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var rows = entities.Select(ToItemRow).ToList();
        return Page(rows, total, page, pageSize);
    }

    public Task<Item?> GetItemAsync(int id) =>
        _db.Items.AsNoTracking().Include(x => x.Product)
            .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.WarehouseStocks).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult> CreateItemsAsync(
        int productId,
        IReadOnlyList<ProductItemInput>? itemInputs)
    {
        itemInputs ??= [];
        if (!await _db.Products.AnyAsync(x => x.Id == productId && x.IsActive))
            return ServiceResult.Fail("Sản phẩm không tồn tại hoặc đã bị vô hiệu hóa.");
        if (itemInputs.Count == 0)
            return ServiceResult.Fail("Vui lòng thêm ít nhất một dòng phân loại.");
        if (itemInputs.Count > 200)
            return ServiceResult.Fail("Mỗi lần chỉ được tạo tối đa 200 phân loại.");

        var normalizedItems = itemInputs.Select((item, index) => new
        {
            RowNumber = index + 1,
            item.CostPrice,
            item.SalePrice,
            AttributeValueIds = item.AttributeValueIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList()
        }).ToList();

        var invalidPriceRow = normalizedItems.FirstOrDefault(x => x.CostPrice < 0 || x.SalePrice < 0);
        if (invalidPriceRow is not null)
            return ServiceResult.Fail($"Giá tại dòng {invalidPriceRow.RowNumber} không được âm.");

        var selectedValueIds = normalizedItems
            .SelectMany(x => x.AttributeValueIds)
            .Distinct()
            .ToList();
        var values = await _db.AttributeValues.AsNoTracking()
            .Where(x => selectedValueIds.Contains(x.Id) && x.IsActive && x.Attribute.IsActive)
            .Select(x => new { x.Id, x.AttributeId })
            .ToListAsync();
        if (values.Count != selectedValueIds.Count)
            return ServiceResult.Fail("Có giá trị thuộc tính không hợp lệ hoặc đã bị vô hiệu hóa.");

        var attributeByValue = values.ToDictionary(x => x.Id, x => x.AttributeId);
        foreach (var item in normalizedItems)
        {
            if (item.AttributeValueIds
                .Select(x => attributeByValue[x])
                .GroupBy(x => x)
                .Any(x => x.Count() > 1))
            {
                return ServiceResult.Fail(
                    $"Dòng {item.RowNumber} chỉ được chọn một giá trị trong mỗi nhóm thuộc tính.");
            }
        }

        var duplicateCombination = normalizedItems
            .GroupBy(x => string.Join(",", x.AttributeValueIds))
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateCombination is not null)
        {
            var duplicatedRows = string.Join(", ", duplicateCombination.Select(x => x.RowNumber));
            return ServiceResult.Fail($"Các dòng {duplicatedRows} đang trùng tổ hợp thuộc tính.");
        }

        var existingItemIds = await _db.Items.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Select(x => x.Id)
            .ToListAsync();
        var existingPairs = await _db.ItemAttributes.AsNoTracking()
            .Where(x => existingItemIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.AttributeValueId })
            .ToListAsync();
        foreach (var existingItemId in existingItemIds)
        {
            var existingSet = existingPairs
                .Where(x => x.ItemId == existingItemId)
                .Select(x => x.AttributeValueId)
                .OrderBy(x => x);
            var duplicateRow = normalizedItems.FirstOrDefault(x =>
                existingSet.SequenceEqual(x.AttributeValueIds));
            if (duplicateRow is not null)
            {
                return ServiceResult.Fail(
                    $"Tổ hợp thuộc tính tại dòng {duplicateRow.RowNumber} đã tồn tại trong sản phẩm.");
            }
        }

        try
        {
            var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in normalizedItems)
            {
                string itemCode;
                do
                {
                    itemCode = await GenerateItemCodeAsync();
                } while (!reservedCodes.Add(itemCode));

                _db.Items.Add(new Item
                {
                    ProductId = productId,
                    Code = itemCode,
                    CostPrice = input.CostPrice,
                    SalePrice = input.SalePrice,
                    IsActive = true,
                    ItemAttributes = input.AttributeValueIds
                        .Select(x => new ItemAttribute { AttributeValueId = x })
                        .ToList()
                });
            }

            await _db.SaveChangesAsync();
            return ServiceResult.Ok($"Đã tạo {normalizedItems.Count} phân loại.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể tạo danh sách Item cho sản phẩm {ProductId}", productId);
            return ServiceResult.Fail("Không thể tạo danh sách phân loại. Vui lòng kiểm tra dữ liệu trùng.");
        }
    }

    public async Task<ServiceResult> SaveItemAsync(Item model, List<int>? attributeValueIds)
    {
        model.Code = NormalizeCode(model.Code);
        model.Barcode = CleanNullable(model.Barcode);
        attributeValueIds = (attributeValueIds ?? []).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
        if (!await _db.Products.AnyAsync(x => x.Id == model.ProductId && x.IsActive))
            return ServiceResult.Fail("Sản phẩm không tồn tại hoặc đã bị vô hiệu hóa.");
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            model.Code = model.Id == 0
                ? await GenerateItemCodeAsync()
                : await _db.Items.AsNoTracking().Where(x => x.Id == model.Id).Select(x => x.Code)
                    .FirstOrDefaultAsync() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(model.Code)) return ServiceResult.Fail("Không thể tạo dữ liệu phân loại.");
        if (model.CostPrice < 0 || model.SalePrice < 0) return ServiceResult.Fail("Giá không được âm.");
        if (await _db.Items.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
            return ServiceResult.Fail("Phân loại đã tồn tại.");
        if (model.Barcode is not null &&
            await _db.Items.AnyAsync(x => x.Barcode == model.Barcode && x.Id != model.Id))
            return ServiceResult.Fail("Barcode đã tồn tại.");

        var values = await _db.AttributeValues.AsNoTracking()
            .Where(x => attributeValueIds.Contains(x.Id) && x.IsActive && x.Attribute.IsActive)
            .Select(x => new { x.Id, x.AttributeId }).ToListAsync();
        if (values.Count != attributeValueIds.Count)
            return ServiceResult.Fail("Có giá trị thuộc tính không hợp lệ hoặc đã bị vô hiệu hóa.");
        if (values.GroupBy(x => x.AttributeId).Any(x => x.Count() > 1))
            return ServiceResult.Fail("Mỗi nhóm thuộc tính chỉ được chọn một giá trị.");

        var otherItemIds = await _db.Items.AsNoTracking()
            .Where(x => x.ProductId == model.ProductId && x.Id != model.Id)
            .Select(x => x.Id).ToListAsync();
        var pairs = await _db.ItemAttributes.AsNoTracking().Where(x => otherItemIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.AttributeValueId }).ToListAsync();
        foreach (var otherId in otherItemIds)
        {
            var otherSet = pairs.Where(x => x.ItemId == otherId).Select(x => x.AttributeValueId).OrderBy(x => x);
            if (otherSet.SequenceEqual(attributeValueIds))
                return ServiceResult.Fail("Tổ hợp thuộc tính này đã tồn tại trong sản phẩm.");
        }

        try
        {
            if (model.Id == 0)
            {
                model.IsActive = true;
                model.ItemAttributes = attributeValueIds
                    .Select(x => new ItemAttribute { AttributeValueId = x }).ToList();
                _db.Items.Add(model);
            }
            else
            {
                var entity = await _db.Items.Include(x => x.ItemAttributes).FirstOrDefaultAsync(x => x.Id == model.Id);
                if (entity is null) return ServiceResult.Fail("Không tìm thấy phân loại.");
                entity.ProductId = model.ProductId;
                entity.Code = model.Code;
                entity.Barcode = model.Barcode;
                entity.CostPrice = model.CostPrice;
                entity.SalePrice = model.SalePrice;
                entity.IsActive = model.IsActive;
                _db.ItemAttributes.RemoveRange(entity.ItemAttributes);
                entity.ItemAttributes = attributeValueIds
                    .Select(x => new ItemAttribute { ItemId = entity.Id, AttributeValueId = x }).ToList();
            }

            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã lưu phân loại.", model.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể lưu Item {ItemId}", model.Id);
            return ServiceResult.Fail("Không thể lưu Item. Vui lòng kiểm tra dữ liệu trùng.");
        }
    }

    private async Task<string> GenerateItemCodeAsync()
    {
        string code;
        do
        {
            code = $"SKU-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        } while (await _db.Items.AnyAsync(x => x.Code == code));

        return code;
    }

    private async Task<string> GenerateProductCodeAsync()
    {
        string code;
        do
        {
            code = $"PRD-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        } while (await _db.Products.AnyAsync(x => x.Code == code));

        return code;
    }

    public async Task<ServiceResult> DeleteItemAsync(int id)
    {
        var entity = await _db.Items.Include(x => x.ItemAttributes).Include(x => x.WarehouseStocks)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy phân loại.");
        var used = await _db.StockDocumentDetails.AnyAsync(x => x.ItemId == id);
        if (used)
            return ServiceResult.Fail("Phân loại đã phát sinh giao dịch nên không thể xóa.");
        if (entity.WarehouseStocks.Any(x => x.Quantity != 0))
            return ServiceResult.Fail("Phân loại vẫn còn tồn kho nên không thể xóa.");

        try
        {
            _db.WarehouseStocks.RemoveRange(entity.WarehouseStocks);
            _db.Items.Remove(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa phân loại.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa phân loại {ItemId}", id);
            return ServiceResult.Fail("Không thể xóa phân loại vì đang được dữ liệu khác sử dụng.");
        }
    }

    public async Task<string> GetItemDisplayNameAsync(int id)
    {
        var item = await GetItemAsync(id);
        if (item is null) return string.Empty;
        var attributes = FormatAttributes(item.ItemAttributes);
        return string.IsNullOrEmpty(attributes) ? item.Product.Name : $"{item.Product.Name} - {attributes}";
    }

    public async Task<List<ItemSelection>> GetItemsForSelectionAsync(
        int? productId, int? warehouseId, string? keyword, bool inStockOnly = false,
        IReadOnlyCollection<int>? includeItemIds = null, bool useCostPrice = false)
    {
        var query = _db.Items.AsNoTracking().Where(x => x.IsActive)
            .Include(x => x.Product)
            .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.WarehouseStocks).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        if (inStockOnly)
        {
            if (!warehouseId.HasValue) return [];
            query = query.Where(x => x.WarehouseStocks.Any(s =>
                s.WarehouseId == warehouseId.Value && s.Quantity > 0));
        }
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Code.Contains(keyword) || x.Product.Name.Contains(keyword));
        IOrderedQueryable<Item> orderedQuery = warehouseId.HasValue
            ? query.OrderByDescending(x => x.Product.CreatedAt)
                .ThenByDescending(x => x.WarehouseStocks.Any(stock =>
                    stock.WarehouseId == warehouseId.Value && stock.Quantity > 0))
            : query.OrderByDescending(x => x.Product.CreatedAt)
                .ThenByDescending(x => x.WarehouseStocks.Any(stock => stock.Quantity > 0));
        var items = await orderedQuery
            .ThenByDescending(x => x.ProductId)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ThenBy(x => x.Code)
            .Take(100)
            .ToListAsync();
        var missingItemIds = (includeItemIds ?? [])
            .Where(id => id > 0 && items.All(item => item.Id != id))
            .Distinct()
            .ToList();
        if (missingItemIds.Count > 0)
        {
            var includedItems = await query.Where(x => missingItemIds.Contains(x.Id)).ToListAsync();
            items.AddRange(includedItems);
        }

        var selectedProductIds = items.Select(x => x.ProductId).ToHashSet();
        var colorValueIdByItem = items.ToDictionary(
            item => item.Id,
            item => item.ItemAttributes
                .Where(attribute =>
                    attribute.AttributeValue.Attribute.Name.Contains("màu", StringComparison.OrdinalIgnoreCase) ||
                    attribute.AttributeValue.Attribute.Name.Contains("mau", StringComparison.OrdinalIgnoreCase) ||
                    attribute.AttributeValue.Attribute.Name.Contains("color", StringComparison.OrdinalIgnoreCase))
                .Select(attribute => (int?)attribute.AttributeValueId)
                .FirstOrDefault());
        var imageRows = await _db.ProductImages.AsNoTracking()
            .Include(image => image.ItemAssignments)
            .Where(image => selectedProductIds.Contains(image.ProductId))
            .ToListAsync();
        var imagePathByItem = items.ToDictionary(
            item => item.Id,
            item => imageRows
                .Where(image => image.ItemAssignments.Any(x => x.ItemId == item.Id) ||
                                image.ItemId == item.Id ||
                                (colorValueIdByItem[item.Id].HasValue && image.ColorValueId == colorValueIdByItem[item.Id]) ||
                                (image.ItemAssignments.Count == 0 && !image.ItemId.HasValue &&
                                 !image.ColorValueId.HasValue && image.ProductId == item.ProductId))
                .OrderBy(image => image.ItemAssignments.Any(x => x.ItemId == item.Id) ? 0 :
                                  image.ItemId == item.Id ||
                                  (colorValueIdByItem[item.Id].HasValue && image.ColorValueId == colorValueIdByItem[item.Id]) ? 1 : 2)
                .ThenByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(image => image.RelativePath)
                .FirstOrDefault());
        return items.Select(x => new ItemSelection
        {
            Id = x.Id,
            Code = x.Code,
            DisplayName = string.IsNullOrEmpty(FormatAttributes(x.ItemAttributes))
                ? x.Product.Name
                : $"{x.Product.Name} - {FormatAttributes(x.ItemAttributes)}",
            Price = useCostPrice ? x.CostPrice : x.SalePrice,
            Stock = warehouseId.HasValue
                ? x.WarehouseStocks.Where(s => s.WarehouseId == warehouseId).Sum(s => s.Quantity)
                : x.WarehouseStocks.Sum(s => s.Quantity),
            ImagePath = imagePathByItem.GetValueOrDefault(x.Id)
        }).ToList();
    }

    #endregion

    #region Warehouses and stock

    public Task<List<Warehouse>> GetWarehousesAsync(bool activeOnly = false)
    {
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return query.OrderBy(x => x.Name).ToListAsync();
    }

    public static int? ResolveSingleWarehouseId(IReadOnlyList<Warehouse> warehouses, int? selectedId) =>
        selectedId ?? (warehouses.Count == 1 ? warehouses[0].Id : null);

    public async Task<PagedResult<Warehouse>> GetWarehousesPagedAsync(
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) ||
                                     (x.Address != null && x.Address.Contains(keyword)));
        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            ("address", true) => query.OrderByDescending(x => x.Address),
            ("address", false) => query.OrderBy(x => x.Address),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            ("status", false) => query.OrderBy(x => x.IsActive),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<Warehouse?> GetWarehouseAsync(int id) =>
        _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult> SaveWarehouseAsync(Warehouse model)
    {
        Warehouse? entity = null;
        if (model.Id != 0)
        {
            entity = await _db.Warehouses.FindAsync(model.Id);
            if (entity is null) return ServiceResult.Fail("Không tìm thấy kho.");
        }

        model.Code = NormalizeCode(model.Code);
        if (string.IsNullOrWhiteSpace(model.Code))
            model.Code = entity?.Code ?? await GenerateWarehouseCodeAsync();
        model.Name = Clean(model.Name);
        model.Address = CleanNullable(model.Address);
        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult.Fail("Tên kho là bắt buộc.");
        if (await _db.Warehouses.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
            return ServiceResult.Fail("Mã kho đã tồn tại.");
        if (model.Id == 0)
        {
            model.IsActive = true;
            _db.Warehouses.Add(model);
        }
        else
        {
            entity!.Code = model.Code;
            entity.Name = model.Name;
            entity.Address = model.Address;
            entity.IsActive = model.IsActive;
        }
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã lưu kho.", model.Id);
    }

    private async Task<string> GenerateWarehouseCodeAsync()
    {
        string code;
        do
        {
            code = $"WH-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        } while (await _db.Warehouses.AnyAsync(x => x.Code == code));

        return code;
    }

    public async Task<ServiceResult> DeleteWarehouseAsync(int id)
    {
        var warehouse = await _db.Warehouses
            .Include(x => x.Stocks)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (warehouse is null) return ServiceResult.Fail("Không tìm thấy kho.");

        var hasDocuments = await _db.StockDocuments.AnyAsync(x =>
            x.FromWarehouseId == id || x.ToWarehouseId == id);
        if (hasDocuments)
            return ServiceResult.Fail("Kho đã phát sinh chứng từ nên không thể xóa.");
        if (warehouse.Stocks.Any(x => x.Quantity != 0))
            return ServiceResult.Fail("Kho vẫn còn tồn hàng nên không thể xóa.");

        try
        {
            _db.WarehouseStocks.RemoveRange(warehouse.Stocks);
            _db.Warehouses.Remove(warehouse);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa kho.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa kho {WarehouseId}", id);
            return ServiceResult.Fail("Không thể xóa kho vì đang được dữ liệu khác sử dụng.");
        }
    }

    public async Task<decimal> GetWarehouseStockAsync(int warehouseId, int itemId) =>
        await _db.WarehouseStocks.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.ItemId == itemId)
            .Select(x => (decimal?)x.Quantity).FirstOrDefaultAsync() ?? 0;

    public async Task<StockMatrix> GetStockMatrixAsync(
        int? productId, string? keyword, int? warehouseId = null,
        string? sort = null, string? direction = null)
    {
        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsActive && (!warehouseId.HasValue || x.Id == warehouseId.Value))
            .OrderBy(x => x.Name).ToListAsync();
        var warehouseIds = warehouses.Select(x => x.Id).ToList();
        var query = _db.Items.AsNoTracking().Include(x => x.Product)
            .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.WarehouseStocks.Where(s => warehouseIds.Contains(s.WarehouseId))).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Product.Name.Contains(keyword) ||
                                     (x.Product.Category != null && x.Product.Category.Contains(keyword)) ||
                                     x.ItemAttributes.Any(attribute => attribute.AttributeValue.Value.Contains(keyword)));
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("product", true) => query.OrderByDescending(x => x.Product.Name),
            ("product", false) => query.OrderBy(x => x.Product.Name),
            ("category", true) => query.OrderByDescending(x => x.Product.Category),
            ("category", false) => query.OrderBy(x => x.Product.Category),
            ("unit", true) => query.OrderByDescending(x => x.Product.Unit),
            ("unit", false) => query.OrderBy(x => x.Product.Unit),
            ("variants", true) => query.OrderByDescending(x => x.Product.Items.Count),
            ("variants", false) => query.OrderBy(x => x.Product.Items.Count),
            ("stock", true) or ("status", true) => query.OrderByDescending(x =>
                x.Product.Items.SelectMany(i => i.WarehouseStocks)
                    .Where(s => !warehouseId.HasValue || s.WarehouseId == warehouseId.Value)
                    .Sum(s => s.Quantity)),
            ("stock", false) or ("status", false) => query.OrderBy(x =>
                x.Product.Items.SelectMany(i => i.WarehouseStocks)
                    .Where(s => !warehouseId.HasValue || s.WarehouseId == warehouseId.Value)
                    .Sum(s => s.Quantity)),
            ("value", true) => query.OrderByDescending(x =>
                x.Product.Items.SelectMany(i => i.WarehouseStocks)
                    .Where(s => !warehouseId.HasValue || s.WarehouseId == warehouseId.Value)
                    .Sum(s => s.Quantity * s.Item.CostPrice)),
            ("value", false) => query.OrderBy(x =>
                x.Product.Items.SelectMany(i => i.WarehouseStocks)
                    .Where(s => !warehouseId.HasValue || s.WarehouseId == warehouseId.Value)
                    .Sum(s => s.Quantity * s.Item.CostPrice)),
            _ => query.OrderByDescending(x => x.Product.CreatedAt)
                .ThenByDescending(x => x.Product.Items.SelectMany(i => i.WarehouseStocks)
                    .Where(s => !warehouseId.HasValue || s.WarehouseId == warehouseId.Value)
                    .Any(s => s.Quantity > 0))
                .ThenByDescending(x => x.ProductId)
        };
        var items = await ordered
            .ThenByDescending(x => x.ProductId)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var rows = items.Select(item => new StockMatrixRow
        {
            ItemId = item.Id,
            ProductId = item.ProductId,
            ProductName = item.Product.Name,
            ProductCategory = item.Product.Category,
            ProductUnit = item.Product.Unit,
            ProductCreatedAt = item.Product.CreatedAt,
            ItemCode = item.Code,
            Attributes = FormatAttributes(item.ItemAttributes),
            CostPrice = item.CostPrice,
            Quantities = warehouses.ToDictionary(
                warehouse => warehouse.Id,
                warehouse => item.WarehouseStocks.Where(x => x.WarehouseId == warehouse.Id).Sum(x => x.Quantity))
        }).ToList();
        return new StockMatrix { Warehouses = warehouses, Rows = rows };
    }

    public Task<List<WarehouseStock>> GetLowStockAsync(decimal threshold) =>
        _db.WarehouseStocks.AsNoTracking().Include(x => x.Warehouse)
            .Include(x => x.Item).ThenInclude(x => x.Product)
            .Where(x => x.Quantity <= threshold && x.Warehouse.IsActive && x.Item.IsActive)
            .OrderBy(x => x.Quantity).Take(100).ToListAsync();

    public async Task<List<NegativeStockRow>> GetNegativeStocksAsync(
        int? warehouseId = null, string? keyword = null)
    {
        var query = _db.WarehouseStocks.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Item).ThenInclude(x => x.Product)
            .Include(x => x.Item).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Where(x => x.Quantity < 0);

        if (warehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        keyword = CleanNullable(keyword);
        if (keyword is not null)
        {
            query = query.Where(x =>
                x.Item.Product.Name.Contains(keyword) ||
                (x.Item.Product.Category != null && x.Item.Product.Category.Contains(keyword)) ||
                x.Item.ItemAttributes.Any(attribute => attribute.AttributeValue.Value.Contains(keyword)) ||
                x.Warehouse.Name.Contains(keyword));
        }

        var stocks = await query.ToListAsync();
        if (stocks.Count == 0) return [];

        var itemIds = stocks.Select(x => x.ItemId).Distinct().ToList();
        var warehouseIds = stocks.Select(x => x.WarehouseId).Distinct().ToList();
        var saleFacts = await _db.StockDocumentDetails.AsNoTracking()
            .Where(x => itemIds.Contains(x.ItemId) &&
                        x.Document.FromWarehouseId.HasValue &&
                        warehouseIds.Contains(x.Document.FromWarehouseId.Value) &&
                        x.Document.DocumentType == StockDocumentType.Sale &&
                        x.Document.Status == DocumentStatus.Completed)
            .GroupBy(x => new { WarehouseId = x.Document.FromWarehouseId!.Value, x.ItemId })
            .Select(group => new
            {
                group.Key.WarehouseId,
                group.Key.ItemId,
                LastSaleAt = group.Max(x => x.Document.DocumentDate),
                SaleOrderCount = group.Select(x => x.DocumentId).Distinct().Count()
            })
            .ToListAsync();
        var saleFactsByStock = saleFacts.ToDictionary(x => (x.WarehouseId, x.ItemId));

        return stocks.Select(stock =>
            {
                saleFactsByStock.TryGetValue((stock.WarehouseId, stock.ItemId), out var saleFact);
                return new NegativeStockRow
                {
                    WarehouseId = stock.WarehouseId,
                    WarehouseName = stock.Warehouse.Name,
                    ItemId = stock.ItemId,
                    ProductId = stock.Item.ProductId,
                    ProductName = stock.Item.Product.Name,
                    ProductUnit = stock.Item.Product.Unit,
                    Attributes = FormatAttributes(stock.Item.ItemAttributes),
                    Quantity = stock.Quantity,
                    CostPrice = stock.Item.CostPrice,
                    LastSaleAt = saleFact?.LastSaleAt,
                    SaleOrderCount = saleFact?.SaleOrderCount ?? 0
                };
            })
            .OrderByDescending(x => x.LastSaleAt)
            .ThenByDescending(x => x.MissingQuantity)
            .ThenBy(x => x.ProductName)
            .ToList();
    }

    #endregion

    #region Customers

    public async Task<PagedResult<Customer>> GetCustomersAsync(
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Customers.AsNoTracking().AsQueryable();
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Name.Contains(keyword) ||
                                     (x.Phone != null && x.Phone.Contains(keyword)));
        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            ("type", true) => query.OrderByDescending(x => x.CustomerType),
            ("type", false) => query.OrderBy(x => x.CustomerType),
            ("phone", true) => query.OrderByDescending(x => x.Phone),
            ("phone", false) => query.OrderBy(x => x.Phone),
            ("address", true) => query.OrderByDescending(x => x.Address),
            ("address", false) => query.OrderBy(x => x.Address),
            ("debt", true) => query.OrderByDescending(x => x.Debt),
            ("debt", false) => query.OrderBy(x => x.Debt),
            ("status", true) => query.OrderByDescending(x => x.IsActive),
            ("status", false) => query.OrderBy(x => x.IsActive),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<Customer?> GetCustomerAsync(int id) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<Customer>> GetActiveCustomersAsync() =>
        _db.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();

    public async Task<ServiceResult> SaveCustomerAsync(Customer model)
    {
        Customer? entity = null;
        if (model.Id != 0)
        {
            entity = await _db.Customers.FindAsync(model.Id);
            if (entity is null) return ServiceResult.Fail("Không tìm thấy khách hàng.");
        }

        model.Code = NormalizeCode(model.Code);
        if (string.IsNullOrWhiteSpace(model.Code))
            model.Code = entity?.Code ?? await GenerateCustomerCodeAsync();
        model.Name = Clean(model.Name);
        model.Phone = CleanNullable(model.Phone);
        model.Address = CleanNullable(model.Address);
        model.TaxCode = CleanNullable(model.TaxCode);
        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult.Fail("Tên khách hàng là bắt buộc.");
        if (await _db.Customers.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
            return ServiceResult.Fail("Mã khách hàng đã tồn tại.");
        if (model.Id == 0)
        {
            model.Debt = 0;
            model.IsActive = true;
            _db.Customers.Add(model);
        }
        else
        {
            entity!.Code = model.Code;
            entity.Name = model.Name;
            entity.CustomerType = model.CustomerType;
            entity.Phone = model.Phone;
            entity.Address = model.Address;
            entity.TaxCode = model.TaxCode;
            entity.IsActive = model.IsActive;
        }
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã lưu khách hàng.", model.Id);
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        string code;
        do
        {
            code = $"CUS-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        } while (await _db.Customers.AnyAsync(x => x.Code == code));

        return code;
    }

    public async Task<ServiceResult> DeleteCustomerAsync(int id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy khách hàng.");
        if (await _db.StockDocuments.AnyAsync(x => x.CustomerId == id) ||
            await _db.Payments.AnyAsync(x => x.CustomerId == id))
            return ServiceResult.Fail("Khách hàng đã phát sinh chứng từ hoặc thanh toán nên không thể xóa.");
        if (entity.Debt != 0)
            return ServiceResult.Fail("Khách hàng vẫn còn công nợ nên không thể xóa.");

        try
        {
            _db.Customers.Remove(entity);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã xóa khách hàng.", id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa khách hàng {CustomerId}", id);
            return ServiceResult.Fail("Không thể xóa khách hàng vì đang được dữ liệu khác sử dụng.");
        }
    }

    public async Task<decimal> GetCustomerDebtAsync(int customerId) =>
        await _db.Customers.AsNoTracking().Where(x => x.Id == customerId)
            .Select(x => (decimal?)x.Debt).FirstOrDefaultAsync() ?? 0;

    public async Task<List<CustomerStatementRow>> GetCustomerStatementAsync(
        int customerId, DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate?.Date ?? DateTime.MinValue;
        var to = (toDate?.Date.AddDays(1)) ?? DateTime.MaxValue;
        var sales = await _db.StockDocuments.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Status == DocumentStatus.Completed &&
                        (x.DocumentType == StockDocumentType.Sale || x.DocumentType == StockDocumentType.Return) &&
                        x.DocumentDate >= from && x.DocumentDate < to)
            .Select(x => new CustomerStatementRow
            {
                Date = x.DocumentDate,
                Reference = x.DocumentNo,
                Description = x.DocumentType == StockDocumentType.Sale ? "Bán hàng" : "Trả hàng",
                Debit = x.DocumentType == StockDocumentType.Sale ? x.DebtAmount : 0,
                Credit = x.DocumentType == StockDocumentType.Return ? x.DebtAmount : 0
            }).ToListAsync();
        var payments = await _db.Payments.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Status == PaymentStatus.Completed &&
                        x.PaymentDate >= from && x.PaymentDate < to)
            .Select(x => new CustomerStatementRow
            {
                Date = x.PaymentDate,
                Reference = "PT-" + x.Id,
                Description = "Thanh toán",
                Credit = x.Amount
            }).ToListAsync();
        return sales.Concat(payments).OrderByDescending(x => x.Date).ToList();
    }

    #endregion

    #region Documents

    public async Task<PagedResult<StockDocument>> GetDocumentsAsync(
        StockDocumentType? type, DocumentStatus? status, DateTime? fromDate, DateTime? toDate,
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.StockDocuments.AsNoTracking().Include(x => x.Customer)
            .Include(x => x.FromWarehouse).Include(x => x.ToWarehouse).AsQueryable();
        if (type.HasValue) query = query.Where(x => x.DocumentType == type.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (fromDate.HasValue) query = query.Where(x => x.DocumentDate >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.DocumentDate < end);
        }
        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.DocumentNo.Contains(keyword) ||
                                     (x.Customer != null && x.Customer.Name.Contains(keyword)));
        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("number", true) => query.OrderByDescending(x => x.DocumentNo),
            ("number", false) => query.OrderBy(x => x.DocumentNo),
            ("date", false) => query.OrderBy(x => x.DocumentDate),
            ("fromwarehouse", true) => query.OrderByDescending(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("fromwarehouse", false) => query.OrderBy(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("towarehouse", true) => query.OrderByDescending(x => x.ToWarehouse != null ? x.ToWarehouse.Name : null),
            ("towarehouse", false) => query.OrderBy(x => x.ToWarehouse != null ? x.ToWarehouse.Name : null),
            ("customer", true) => query.OrderByDescending(x => x.Customer != null ? x.Customer.Name : null),
            ("customer", false) => query.OrderBy(x => x.Customer != null ? x.Customer.Name : null),
            ("total", true) => query.OrderByDescending(x => x.TotalAmount),
            ("total", false) => query.OrderBy(x => x.TotalAmount),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("status", false) => query.OrderBy(x => x.Status),
            _ => query.OrderByDescending(x => x.DocumentDate)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<PagedResult<StockDocument>> GetImportHistoryAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword,
        int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null) =>
        GetStockOperationHistoryAsync(StockDocumentType.Import, fromDate, toDate, keyword, page, pageSize, sort, direction);

    public Task<PagedResult<StockDocument>> GetExportHistoryAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword,
        int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null) =>
        GetStockOperationHistoryAsync(StockDocumentType.Export, fromDate, toDate, keyword, page, pageSize, sort, direction);

    public Task<PagedResult<StockDocument>> GetTransferHistoryAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword,
        int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null) =>
        GetStockOperationHistoryAsync(StockDocumentType.Transfer, fromDate, toDate, keyword, page, pageSize, sort, direction);

    public async Task<PagedResult<StockDocument>> GetSalesHistoryAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword,
        int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null,
        bool includePending = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.StockDocuments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.FromWarehouse)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.Product)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .AsSplitQuery()
            .AsQueryable();

        query = ApplySalesHistoryFilters(query, fromDate, toDate, keyword, includePending);

        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("customer", true) => query.OrderByDescending(x => x.Customer != null ? x.Customer.Name : null),
            ("customer", false) => query.OrderBy(x => x.Customer != null ? x.Customer.Name : null),
            ("warehouse", true) => query.OrderByDescending(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("warehouse", false) => query.OrderBy(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("date", false) => query.OrderBy(x => x.DocumentDate),
            ("total", true) => query.OrderByDescending(x => x.TotalAmount),
            ("total", false) => query.OrderBy(x => x.TotalAmount),
            ("payment", true) => query.OrderByDescending(x => x.DebtAmount),
            ("payment", false) => query.OrderBy(x => x.DebtAmount),
            _ => query.OrderByDescending(x => x.DocumentDate)
        };
        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Page(items, total, page, pageSize);
    }

    public async Task<(decimal TotalAmount, decimal DebtAmount, int PendingCount)> GetSalesHistorySummaryAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword)
    {
        var query = ApplySalesHistoryFilters(
            _db.StockDocuments.AsNoTracking(), fromDate, toDate, keyword, includePending: true);
        var summary = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalAmount = group.Sum(x => x.Status == DocumentStatus.Completed ? x.TotalAmount : 0),
                DebtAmount = group.Sum(x => x.Status == DocumentStatus.Completed ? x.DebtAmount : 0),
                PendingCount = group.Count(x =>
                    x.Status == DocumentStatus.Draft || x.Status == DocumentStatus.Invoiced)
            })
            .SingleOrDefaultAsync();

        return summary is null ? (0, 0, 0) : (summary.TotalAmount, summary.DebtAmount, summary.PendingCount);
    }

    private static IQueryable<StockDocument> ApplySalesHistoryFilters(
        IQueryable<StockDocument> query,
        DateTime? fromDate, DateTime? toDate, string? keyword,
        bool includePending = false)
    {
        query = query.Where(x => x.DocumentType == StockDocumentType.Sale &&
                                 (x.Status == DocumentStatus.Completed ||
                                  (includePending && (x.Status == DocumentStatus.Draft ||
                                                      x.Status == DocumentStatus.Invoiced))));

        if (fromDate.HasValue)
            query = query.Where(x => x.DocumentDate >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.DocumentDate < end);
        }

        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                (x.CustomerPhone != null && x.CustomerPhone.Contains(keyword)) ||
                (x.Customer != null &&
                    (x.Customer.Name.Contains(keyword) ||
                     (x.Customer.Phone != null && x.Customer.Phone.Contains(keyword)))) ||
                (x.FromWarehouse != null && x.FromWarehouse.Name.Contains(keyword)) ||
                (x.CreatedBy != null && x.CreatedBy.Contains(keyword)) ||
                x.Details.Any(detail =>
                    detail.Item.Product.Name.Contains(keyword) ||
                    (detail.Item.Product.Category != null && detail.Item.Product.Category.Contains(keyword)) ||
                    detail.Item.ItemAttributes.Any(attribute =>
                        attribute.AttributeValue.Value.Contains(keyword))));
        }

        return query;
    }

    private async Task<PagedResult<StockDocument>> GetStockOperationHistoryAsync(
        StockDocumentType documentType, DateTime? fromDate, DateTime? toDate, string? keyword,
        int page, int pageSize, string? sort, string? direction)
    {
        if (documentType is not (StockDocumentType.Import or StockDocumentType.Export or StockDocumentType.Transfer))
            throw new ArgumentOutOfRangeException(nameof(documentType));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.StockDocuments.AsNoTracking()
            .Where(x => x.DocumentType == documentType &&
                        x.Status == DocumentStatus.Completed)
            .Include(x => x.FromWarehouse)
            .Include(x => x.ToWarehouse)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.Product)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .AsSplitQuery()
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(x => x.DocumentDate >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.DocumentDate < end);
        }

        keyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                (x.FromWarehouse != null && x.FromWarehouse.Name.Contains(keyword)) ||
                (x.ToWarehouse != null && x.ToWarehouse.Name.Contains(keyword)) ||
                (x.CreatedBy != null && x.CreatedBy.Contains(keyword)) ||
                (x.Remark != null && x.Remark.Contains(keyword)) ||
                x.Details.Any(detail =>
                    detail.Item.Product.Name.Contains(keyword) ||
                    (detail.Item.Product.Category != null && detail.Item.Product.Category.Contains(keyword)) ||
                    detail.Item.ItemAttributes.Any(attribute =>
                        attribute.AttributeValue.Value.Contains(keyword))));
        }

        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("product", true) => query.OrderByDescending(x => x.Details.Select(d => d.Item.Product.Name).FirstOrDefault()),
            ("product", false) => query.OrderBy(x => x.Details.Select(d => d.Item.Product.Name).FirstOrDefault()),
            ("fromwarehouse", true) => query.OrderByDescending(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("fromwarehouse", false) => query.OrderBy(x => x.FromWarehouse != null ? x.FromWarehouse.Name : null),
            ("towarehouse", true) => query.OrderByDescending(x => x.ToWarehouse != null ? x.ToWarehouse.Name : null),
            ("towarehouse", false) => query.OrderBy(x => x.ToWarehouse != null ? x.ToWarehouse.Name : null),
            ("date", false) => query.OrderBy(x => x.DocumentDate),
            ("person", true) => query.OrderByDescending(x => x.CreatedBy),
            ("person", false) => query.OrderBy(x => x.CreatedBy),
            ("quantity", true) => query.OrderByDescending(x => x.Details.Sum(d => d.Quantity)),
            ("quantity", false) => query.OrderBy(x => x.Details.Sum(d => d.Quantity)),
            ("remark", true) => query.OrderByDescending(x => x.Remark),
            ("remark", false) => query.OrderBy(x => x.Remark),
            ("price", true) => query.OrderByDescending(x => x.Details.Sum(d => d.Price)),
            ("price", false) => query.OrderBy(x => x.Details.Sum(d => d.Price)),
            ("amount", true) => query.OrderByDescending(x => x.Details.Sum(d => d.Amount)),
            ("amount", false) => query.OrderBy(x => x.Details.Sum(d => d.Amount)),
            _ => query.OrderByDescending(x => x.DocumentDate)
        };
        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Page(items, total, page, pageSize);
    }

    public Task<StockDocument?> GetDocumentAsync(long id) =>
        _db.StockDocuments.AsNoTracking().Include(x => x.Customer)
            .Include(x => x.FromWarehouse).Include(x => x.ToWarehouse)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.Product)
            .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.ItemAttributes)
                .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
            .Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id);

    private async Task<ServiceResult> SaveDocumentCoreAsync(DocumentInput input)
    {
        var validation = await ValidateDocumentInputAsync(input);
        if (!validation.Success) return validation;
        var details = NormalizeDetails(input.Details);
        var subtotal = details.Sum(x => x.Quantity * x.Price);
        if (input.DocumentType == StockDocumentType.Adjust)
            subtotal = details.Sum(x => Math.Abs(x.Quantity) * x.Price);
        var discount = input.DocumentType == StockDocumentType.Sale ? input.DiscountAmount : 0;
        if (discount > subtotal)
            return ServiceResult.Fail("Chiết khấu không được vượt tổng tiền đơn hàng.");
        var total = subtotal - discount;
        var previousDebt = 0m;
        if (input.DocumentType == StockDocumentType.Sale && input.CustomerId.HasValue)
        {
            var customerDebt = await _db.Customers.AsNoTracking()
                .Where(x => x.Id == input.CustomerId.Value)
                .Select(x => x.Debt)
                .FirstOrDefaultAsync();
            var trackedDebt = await _db.StockDocuments.AsNoTracking()
                .Where(x => x.Id != input.Id && x.CustomerId == input.CustomerId.Value &&
                            x.DocumentType == StockDocumentType.Sale &&
                            x.Status == DocumentStatus.Completed && x.DebtAmount > 0)
                .SumAsync(x => (decimal?)x.DebtAmount) ?? 0;
            previousDebt = Math.Min(customerDebt, trackedDebt);
        }

        try
        {
            StockDocument document;
            if (input.Id == 0)
            {
                document = new StockDocument
                {
                    DocumentNo = await GenerateDocumentNoAsync(input.DocumentType),
                    DocumentType = input.DocumentType,
                    Status = DocumentStatus.Draft,
                    DocumentDate = DateTime.Now
                };
                _db.StockDocuments.Add(document);
            }
            else
            {
                document = await _db.StockDocuments.Include(x => x.Details)
                    .FirstOrDefaultAsync(x => x.Id == input.Id) ?? null!;
                if (document is null) return ServiceResult.Fail("Không tìm thấy chứng từ.");
                if (document.Status != DocumentStatus.Draft)
                    return ServiceResult.Fail("Chỉ chứng từ nháp mới được sửa.");
                if (document.DocumentType != input.DocumentType)
                    return ServiceResult.Fail("Không được thay đổi loại chứng từ.");
                _db.StockDocumentDetails.RemoveRange(document.Details);
            }

            document.CustomerId = input.CustomerId;
            document.CustomerPhone = input.DocumentType == StockDocumentType.Sale
                ? CleanNullable(input.CustomerPhone)
                : null;
            document.FromWarehouseId = input.FromWarehouseId;
            document.ToWarehouseId = input.ToWarehouseId;
            document.Remark = CleanNullable(input.Remark);
            document.SubtotalAmount = subtotal;
            document.DiscountAmount = discount;
            document.PreviousDebtAmount = previousDebt;
            document.PreviousDebtPaidAmount = 0;
            document.TotalAmount = total;
            document.PaidAmount = 0;
            document.DebtAmount = 0;
            document.Details = details.Select(x => new StockDocumentDetail
            {
                ItemId = x.ItemId,
                Quantity = x.Quantity,
                Price = x.Price,
                Amount = (input.DocumentType == StockDocumentType.Adjust ? Math.Abs(x.Quantity) : x.Quantity) * x.Price
            }).ToList();
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã ghi chứng từ.", document.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể ghi chứng từ {DocumentId}", input.Id);
            return ServiceResult.Fail("Không thể lưu chứng từ. Vui lòng thử lại.");
        }
    }

    public Task<ServiceResult> CreateImportAsync(DocumentInput input) => SaveAsTypeAsync(input, StockDocumentType.Import);
    public Task<ServiceResult> CreateExportAsync(DocumentInput input) => SaveAsTypeAsync(input, StockDocumentType.Export);
    public Task<ServiceResult> CreateTransferAsync(DocumentInput input) => SaveAsTypeAsync(input, StockDocumentType.Transfer);
    public async Task<ServiceResult> CreateSaleAsync(DocumentInput input)
    {
        input.DocumentType = StockDocumentType.Sale;
        input.PaidAmount = 0;
        var isNew = input.Id == 0;
        var result = await SaveDocumentCoreAsync(input);
        if (result.Success)
            result.Message = isNew
                ? "Đã tạo đơn hàng ở trạng thái chờ xử lý."
                : "Đã lưu thay đổi cho đơn hàng chờ xử lý.";
        return result;
    }

    public async Task<ServiceResult> IssueSaleInvoiceAsync(long id)
    {
        var document = await _db.StockDocuments.FirstOrDefaultAsync(x =>
            x.Id == id && x.DocumentType == StockDocumentType.Sale);
        if (document is null) return ServiceResult.Fail("Không tìm thấy đơn hàng.");
        if (document.Status == DocumentStatus.Cancelled)
            return ServiceResult.Fail("Đơn hàng đã bị hủy nên không thể lập phiếu xuất kho.");
        if (document.Status == DocumentStatus.Completed)
            return ServiceResult.Ok("Đơn hàng đã hoàn thành; có thể in lại phiếu xuất kho.", document.Id);
        if (document.Status == DocumentStatus.Draft)
        {
            document.PreviousDebtAmount = document.CustomerId.HasValue
                ? await GetCustomerOutstandingDebtAsync(document.CustomerId.Value)
                : 0;
            document.Status = DocumentStatus.Invoiced;
            await _db.SaveChangesAsync();
        }
        return ServiceResult.Ok(
            "Đã lập phiếu xuất kho. Đơn hàng vẫn đang chờ nhập thanh toán và hoàn thành.",
            document.Id);
    }
    public Task<ServiceResult> CreateReturnAsync(DocumentInput input) => SaveAsTypeAsync(input, StockDocumentType.Return);
    public Task<ServiceResult> CreateAdjustmentAsync(DocumentInput input) => SaveAsTypeAsync(input, StockDocumentType.Adjust);

    public async Task<ServiceResult> SaveAndCompleteDocumentAsync(DocumentInput input)
    {
        try
        {
            if (_db.Database.CurrentTransaction is not null)
            {
                var savedInCurrentTransaction = await SaveDocumentCoreAsync(input);
                return !savedInCurrentTransaction.Success || !savedInCurrentTransaction.Id.HasValue
                    ? savedInCurrentTransaction
                    : await CompleteDocumentCoreAsync(savedInCurrentTransaction.Id.Value);
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var saved = await SaveDocumentCoreAsync(input);
                if (!saved.Success || !saved.Id.HasValue)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    return saved;
                }

                var completed = await CompleteDocumentCoreAsync(saved.Id.Value);
                if (!completed.Success)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    return completed;
                }

                await transaction.CommitAsync();
                return completed;
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Xung đột tồn kho khi lưu và hoàn tất chứng từ {DocumentId}", input.Id);
            return ServiceResult.Fail("Tồn kho vừa thay đổi bởi người dùng khác. Vui lòng tải lại và thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi transaction khi lưu và hoàn tất chứng từ {DocumentId}", input.Id);
            return ServiceResult.Fail("Không thể lưu chứng từ. Toàn bộ giao dịch đã được hoàn tác.");
        }
    }

    public async Task<ServiceResult> CompleteDocumentAsync(long id, decimal? paidAmount = null)
    {
        try
        {
            if (_db.Database.CurrentTransaction is not null)
                return await CompleteDocumentCoreAsync(id, paidAmount);

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var result = await CompleteDocumentCoreAsync(id, paidAmount);
                if (!result.Success)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    return result;
                }
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Xung đột tồn kho khi hoàn tất chứng từ {DocumentId}", id);
            return ServiceResult.Fail("Tồn kho vừa thay đổi bởi người dùng khác. Vui lòng tải lại và thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi transaction khi hoàn tất chứng từ {DocumentId}", id);
            return ServiceResult.Fail("Không thể hoàn tất chứng từ. Giao dịch đã được hoàn tác.");
        }
    }

    private async Task<ServiceResult> CompleteDocumentCoreAsync(long id, decimal? paidAmount = null)
    {
        var document = await _db.StockDocuments.Include(x => x.Details).ThenInclude(x => x.Item)
            .Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
        if (document is null) return ServiceResult.Fail("Không tìm thấy chứng từ.");
        var expectedStatus = document.DocumentType == StockDocumentType.Sale
            ? DocumentStatus.Invoiced
            : DocumentStatus.Draft;
        if (document.Status != expectedStatus)
            return ServiceResult.Fail(document.DocumentType == StockDocumentType.Sale
                ? "Đơn hàng cần được lập phiếu xuất kho trước khi hoàn thành."
                : "Chỉ chứng từ nháp mới được hoàn tất.");
        decimal? receivedAmount = null;
        if (document.DocumentType == StockDocumentType.Sale)
        {
            if (!paidAmount.HasValue)
                return ServiceResult.Fail("Vui lòng nhập số tiền khách đã thanh toán.");
            if (paidAmount.Value < 0)
                return ServiceResult.Fail("Số tiền khách thanh toán không được âm.");
            receivedAmount = paidAmount.Value;
        }
        var input = new DocumentInput
        {
            Id = document.Id,
            DocumentType = document.DocumentType,
            DocumentDate = document.DocumentDate,
            CustomerId = document.CustomerId,
            CustomerPhone = document.CustomerPhone,
            FromWarehouseId = document.FromWarehouseId,
            ToWarehouseId = document.ToWarehouseId,
            DiscountAmount = document.DiscountAmount,
            PaidAmount = receivedAmount ?? document.PaidAmount,
            Details = document.Details.Select(x => new DocumentDetailInput
                { ItemId = x.ItemId, Quantity = x.Quantity, Price = x.Price }).ToList()
        };
        var validation = await ValidateDocumentInputAsync(input);
        if (!validation.Success) return validation;

        document.SubtotalAmount = document.Details.Sum(x =>
            (document.DocumentType == StockDocumentType.Adjust ? Math.Abs(x.Quantity) : x.Quantity) * x.Price);
        if (document.DiscountAmount > document.SubtotalAmount)
            return ServiceResult.Fail("Chiết khấu không được vượt tổng tiền đơn hàng.");
        document.TotalAmount = document.SubtotalAmount - document.DiscountAmount;
        foreach (var detail in document.Details)
            detail.Amount = (document.DocumentType == StockDocumentType.Adjust
                ? Math.Abs(detail.Quantity) : detail.Quantity) * detail.Price;

        var stockResult = await ApplyDocumentStockAsync(document, reverse: false);
        if (!stockResult.Success) return stockResult;

        if (document.DocumentType == StockDocumentType.Import)
        {
            foreach (var detail in document.Details) detail.Item.CostPrice = detail.Price;
        }
        else if (document.DocumentType == StockDocumentType.Sale)
        {
            var customer = document.Customer!;
            var outstandingSales = await _db.StockDocuments
                .Where(x => x.Id != document.Id && x.CustomerId == customer.Id &&
                            x.DocumentType == StockDocumentType.Sale &&
                            x.Status == DocumentStatus.Completed && x.DebtAmount > 0)
                .OrderBy(x => x.DocumentDate)
                .ThenBy(x => x.Id)
                .ToListAsync();
            var trackedDebt = outstandingSales.Sum(x => x.DebtAmount);
            var previousDebt = Math.Min(customer.Debt, trackedDebt);
            var totalPayable = document.TotalAmount + previousDebt;
            var received = receivedAmount!.Value;
            if (received > totalPayable)
                return ServiceResult.Fail("Số tiền khách thanh toán không được vượt tổng phải thanh toán.");

            document.PreviousDebtAmount = previousDebt;
            var remainingForOldDebt = Math.Min(received, previousDebt);
            var oldDebtPaid = 0m;
            var autoPayment = received > 0
                ? new Payment
                {
                    CustomerId = customer.Id,
                    Document = document,
                    Amount = received,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = PaymentMethod.Cash,
                    Status = PaymentStatus.Completed,
                    Remark = "Thanh toán khi bán hàng"
                }
                : null;

            foreach (var outstandingSale in outstandingSales)
            {
                if (remainingForOldDebt <= 0) break;
                var allocated = Math.Min(remainingForOldDebt, outstandingSale.DebtAmount);
                outstandingSale.PaidAmount += allocated;
                outstandingSale.DebtAmount -= allocated;
                remainingForOldDebt -= allocated;
                oldDebtPaid += allocated;
                autoPayment!.Allocations.Add(new PaymentAllocation
                {
                    Document = outstandingSale,
                    Amount = allocated
                });
            }

            var currentOrderPaid = received - oldDebtPaid;
            document.PreviousDebtPaidAmount = oldDebtPaid;
            document.PaidAmount = currentOrderPaid;
            document.DebtAmount = document.TotalAmount - currentOrderPaid;
            customer.Debt = Math.Max(0, customer.Debt - oldDebtPaid) + document.DebtAmount;
            if (autoPayment is not null) _db.Payments.Add(autoPayment);
        }
        else if (document.DocumentType == StockDocumentType.Return)
        {
            var customer = document.Customer!;
            document.DebtAmount = Math.Min(customer.Debt, document.TotalAmount);
            customer.Debt -= document.DebtAmount;
        }

        document.Status = DocumentStatus.Completed;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(
            document.DocumentType == StockDocumentType.Sale
                ? "Đã hoàn tất đơn hàng, cập nhật tồn kho, doanh thu và công nợ. Hàng thiếu (nếu có) đã được ghi vào mục Hàng đang thiếu."
                : "Đã lưu chứng từ và cập nhật tồn kho.",
            document.Id);
    }

    public async Task<ServiceResult> CancelDocumentAsync(long id)
    {
        try
        {
            if (_db.Database.CurrentTransaction is not null)
                return await CancelDocumentCoreAsync(id);

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var result = await CancelDocumentCoreAsync(id);
                if (!result.Success)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    return result;
                }
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Xung đột tồn kho khi hủy chứng từ {DocumentId}", id);
            return ServiceResult.Fail("Tồn kho vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi transaction khi hủy chứng từ {DocumentId}", id);
            return ServiceResult.Fail("Không thể hủy chứng từ. Giao dịch đã được hoàn tác.");
        }
    }

    private async Task<ServiceResult> CancelDocumentCoreAsync(long id)
    {
        var document = await _db.StockDocuments.Include(x => x.Details)
            .Include(x => x.Customer)
            .Include(x => x.Payments).ThenInclude(x => x.Allocations).ThenInclude(x => x.Document)
            .Include(x => x.PaymentAllocations).ThenInclude(x => x.Payment)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (document is null) return ServiceResult.Fail("Không tìm thấy chứng từ.");
        if (document.Status == DocumentStatus.Cancelled)
            return ServiceResult.Fail("Chứng từ đã bị hủy.");
        if (document.Status is DocumentStatus.Draft or DocumentStatus.Invoiced)
        {
            document.Status = DocumentStatus.Cancelled;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok("Đã hủy đơn/chứng từ đang chờ xử lý.", document.Id);
        }

        if (document.DocumentType == StockDocumentType.Sale &&
            (document.Payments.Any(x => x.Remark != "Thanh toán khi bán hàng") ||
             document.PaymentAllocations.Any(x => x.Payment.Status != PaymentStatus.Cancelled)))
            return ServiceResult.Fail("Đơn bán đã có phiếu thu phát sinh. Hãy xóa các phiếu thu trước khi hủy đơn bán.");

        var stockResult = await ApplyDocumentStockAsync(document, reverse: true);
        if (!stockResult.Success) return stockResult;
        if (document.DocumentType == StockDocumentType.Sale && document.Customer is not null)
        {
            var automaticPayments = document.Payments
                .Where(x => x.Remark == "Thanh toán khi bán hàng")
                .ToList();
            var restoredOldDebt = 0m;
            foreach (var allocation in automaticPayments.SelectMany(x => x.Allocations))
            {
                allocation.Document.PaidAmount = Math.Max(0, allocation.Document.PaidAmount - allocation.Amount);
                allocation.Document.DebtAmount += allocation.Amount;
                restoredOldDebt += allocation.Amount;
            }
            document.Customer.Debt = Math.Max(0, document.Customer.Debt - document.DebtAmount) + restoredOldDebt;
            _db.Payments.RemoveRange(automaticPayments);
        }
        else if (document.DocumentType == StockDocumentType.Return && document.Customer is not null)
        {
            document.Customer.Debt += document.DebtAmount;
        }

        document.Status = DocumentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã hủy chứng từ và đảo tồn kho.", document.Id);
    }

    #endregion

    #region Payments

    public async Task<PagedResult<DebtCustomerRow>> GetDebtCustomersAsync(
        string? keyword, int page = 1, int pageSize = DefaultPageSize,
        string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Customers.AsNoTracking().Where(x => x.Debt > 0);
        keyword = CleanNullable(keyword);
        if (keyword is not null)
            query = query.Where(x => x.Name.Contains(keyword) || x.Code!.Contains(keyword) ||
                                     (x.Phone != null && x.Phone.Contains(keyword)));

        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var projected = query.Select(x => new DebtCustomerRow
        {
            CustomerId = x.Id,
            CustomerCode = x.Code ?? string.Empty,
            CustomerName = x.Name,
            Phone = x.Phone,
            CurrentDebt = x.Debt,
            OutstandingSaleCount = x.Documents.Count(d =>
                d.DocumentType == StockDocumentType.Sale &&
                d.Status == DocumentStatus.Completed && d.DebtAmount > 0),
            OldestDebtDate = x.Documents
                .Where(d => d.DocumentType == StockDocumentType.Sale &&
                            d.Status == DocumentStatus.Completed && d.DebtAmount > 0)
                .Select(d => (DateTime?)d.DocumentDate)
                .Min(),
            LatestDebtDate = x.Documents
                .Where(d => d.DocumentType == StockDocumentType.Sale &&
                            d.Status == DocumentStatus.Completed && d.DebtAmount > 0)
                .Select(d => (DateTime?)d.DocumentDate)
                .Max()
        });
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("customer", true) => projected.OrderByDescending(x => x.CustomerName),
            ("customer", false) => projected.OrderBy(x => x.CustomerName),
            ("orders", true) => projected.OrderByDescending(x => x.OutstandingSaleCount),
            ("orders", false) => projected.OrderBy(x => x.OutstandingSaleCount),
            ("oldest", true) => projected.OrderByDescending(x => x.OldestDebtDate),
            ("oldest", false) => projected.OrderBy(x => x.OldestDebtDate),
            ("debt", true) => projected.OrderByDescending(x => x.CurrentDebt),
            ("debt", false) => projected.OrderBy(x => x.CurrentDebt),
            _ => projected.OrderByDescending(x => x.LatestDebtDate)
                .ThenByDescending(x => x.CustomerId)
        };
        var items = await ordered.ThenByDescending(x => x.CustomerId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var customerIds = items.Select(x => x.CustomerId).ToList();
        if (customerIds.Count > 0)
        {
            var sales = await _db.StockDocuments.AsNoTracking()
                .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.Product)
                .Include(x => x.Details).ThenInclude(x => x.Item).ThenInclude(x => x.ItemAttributes)
                    .ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
                .Where(x => x.CustomerId.HasValue && customerIds.Contains(x.CustomerId.Value) &&
                            x.DocumentType == StockDocumentType.Sale &&
                            x.Status == DocumentStatus.Completed && x.DebtAmount > 0)
                .OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.Id)
                .ToListAsync();
            var salesByCustomer = sales.GroupBy(x => x.CustomerId!.Value)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<StockDocument>)x.ToList());
            foreach (var item in items)
                item.OutstandingSales = salesByCustomer.GetValueOrDefault(item.CustomerId) ?? [];
        }

        return Page(items, total, page, pageSize);
    }

    public async Task<(int CustomerCount, decimal TotalDebt)> GetDebtCustomerSummaryAsync(string? keyword)
    {
        var query = _db.Customers.AsNoTracking().Where(x => x.Debt > 0);
        keyword = CleanNullable(keyword);
        if (keyword is not null)
            query = query.Where(x => x.Name.Contains(keyword) || x.Code!.Contains(keyword) ||
                                     (x.Phone != null && x.Phone.Contains(keyword)));
        var summary = await query.GroupBy(_ => 1).Select(x => new
        {
            CustomerCount = x.Count(),
            TotalDebt = x.Sum(customer => customer.Debt)
        }).SingleOrDefaultAsync();
        return summary is null ? (0, 0) : (summary.CustomerCount, summary.TotalDebt);
    }

    public async Task<PagedResult<Payment>> GetPaymentsAsync(
        int? customerId, DateTime? fromDate, DateTime? toDate, int page = 1,
        int pageSize = DefaultPageSize, string? sort = null, string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Payments.AsNoTracking().Include(x => x.Customer).Include(x => x.Document)
            .Include(x => x.Allocations).ThenInclude(x => x.Document).AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.PaymentDate >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.PaymentDate < end);
        }
        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("customer", true) => query.OrderByDescending(x => x.Customer.Name),
            ("customer", false) => query.OrderBy(x => x.Customer.Name),
            ("date", false) => query.OrderBy(x => x.PaymentDate),
            ("sale", true) => query.OrderByDescending(x => x.Document != null ? x.Document.DocumentDate : (DateTime?)null),
            ("sale", false) => query.OrderBy(x => x.Document != null ? x.Document.DocumentDate : (DateTime?)null),
            ("method", true) => query.OrderByDescending(x => x.PaymentMethod),
            ("method", false) => query.OrderBy(x => x.PaymentMethod),
            ("amount", true) => query.OrderByDescending(x => x.Amount),
            ("amount", false) => query.OrderBy(x => x.Amount),
            ("remark", true) => query.OrderByDescending(x => x.Remark),
            ("remark", false) => query.OrderBy(x => x.Remark),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("status", false) => query.OrderBy(x => x.Status),
            _ => query.OrderByDescending(x => x.PaymentDate)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<Payment?> GetPaymentAsync(long id) =>
        _db.Payments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Document)
            .Include(x => x.Allocations).ThenInclude(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult> CreatePaymentAsync(Payment model)
    {
        if (model.Amount <= 0) return ServiceResult.Fail("Số tiền thanh toán phải lớn hơn 0.");
        try
        {
            if (_db.Database.CurrentTransaction is not null)
                return await CreatePaymentCoreAsync(model);
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var result = await CreatePaymentCoreAsync(model);
                if (!result.Success) return result;
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo thanh toán cho khách {CustomerId}", model.CustomerId);
            return ServiceResult.Fail("Không thể ghi nhận thanh toán. Giao dịch đã được hoàn tác.");
        }
    }

    private async Task<ServiceResult> CreatePaymentCoreAsync(Payment model)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == model.CustomerId && x.IsActive);
        if (customer is null)
            return ServiceResult.Fail("Khách hàng không tồn tại hoặc đã ngừng sử dụng.");
        if (customer.Debt <= 0)
            return ServiceResult.Fail("Khách hàng không còn công nợ.");
        if (model.Amount > customer.Debt)
            return ServiceResult.Fail("Số tiền thu vượt công nợ hiện tại của khách hàng.");

        var allocations = model.Allocations.Where(x => x.DocumentId > 0 && x.Amount > 0).ToList();
        if (allocations.Count == 0 && model.DocumentId.HasValue)
            allocations.Add(new PaymentAllocation { DocumentId = model.DocumentId.Value, Amount = model.Amount });
        if (allocations.Select(x => x.DocumentId).Distinct().Count() != allocations.Count)
            return ServiceResult.Fail("Mỗi đơn bán chỉ được chọn một lần.");
        if (allocations.Count > 0 && allocations.Sum(x => x.Amount) != model.Amount)
            return ServiceResult.Fail("Tổng tiền phân bổ cho các đơn không khớp số tiền phiếu thu.");

        if (allocations.Count > 0)
        {
            var documentIds = allocations.Select(x => x.DocumentId).ToList();
            var documents = await _db.StockDocuments.AsNoTracking().Where(x =>
                    documentIds.Contains(x.Id) && x.CustomerId == customer.Id &&
                    x.DocumentType == StockDocumentType.Sale &&
                    x.Status == DocumentStatus.Completed && x.DebtAmount > 0)
                .ToDictionaryAsync(x => x.Id);
            if (documents.Count != documentIds.Count)
                return ServiceResult.Fail("Có đơn bán không còn nợ hoặc không thuộc khách hàng này.");
            if (allocations.Any(x => x.Amount > documents[x.DocumentId].DebtAmount))
                return ServiceResult.Fail("Số tiền thu của một đơn đang vượt phần còn nợ.");
        }

        model.Id = 0;
        model.Customer = null!;
        model.DocumentId = null;
        model.Document = null;
        model.Allocations = allocations;
        model.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
        model.Remark = CleanNullable(model.Remark);
        model.Status = PaymentStatus.Draft;
        _db.Payments.Add(model);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã lập phiếu thu ở trạng thái chờ xử lý.", model.Id);
    }

    public async Task<ServiceResult> IssuePaymentAsync(long id)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == id);
        if (payment is null) return ServiceResult.Fail("Không tìm thấy phiếu thu.");
        if (payment.Remark == "Thanh toán khi bán hàng")
            return ServiceResult.Fail("Khoản thanh toán tự động không cần xuất phiếu thu riêng.");
        if (payment.Status == PaymentStatus.Cancelled)
            return ServiceResult.Fail("Phiếu thu đã bị hủy.");
        if (payment.Status == PaymentStatus.Completed)
            return ServiceResult.Ok("Phiếu thu đã hoàn thành; có thể in lại.", payment.Id);
        if (payment.Status == PaymentStatus.Draft)
        {
            payment.Status = PaymentStatus.Issued;
            await _db.SaveChangesAsync();
        }
        return ServiceResult.Ok("Đã xuất phiếu thu. Công nợ chỉ thay đổi sau khi hoàn thành phiếu.", payment.Id);
    }

    public async Task<ServiceResult> CompletePaymentAsync(long id)
    {
        try
        {
            if (_db.Database.CurrentTransaction is not null)
                return await CompletePaymentCoreAsync(id);
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var result = await CompletePaymentCoreAsync(id);
                if (!result.Success) return result;
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi hoàn thành phiếu thu {PaymentId}", id);
            return ServiceResult.Fail("Không thể hoàn thành phiếu thu. Giao dịch đã được hoàn tác.");
        }
    }

    private async Task<ServiceResult> CompletePaymentCoreAsync(long id)
    {
        var payment = await _db.Payments.Include(x => x.Customer).Include(x => x.Document)
            .Include(x => x.Allocations).ThenInclude(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (payment is null) return ServiceResult.Fail("Không tìm thấy phiếu thu.");
        if (payment.Status != PaymentStatus.Issued)
            return ServiceResult.Fail("Phiếu thu cần được xuất trước khi hoàn thành.");
        if (payment.Customer.Debt <= 0 || payment.Amount > payment.Customer.Debt)
            return ServiceResult.Fail("Công nợ khách hàng đã thay đổi. Vui lòng kiểm tra lại phiếu thu.");
        if (payment.Allocations.Count > 0)
        {
            if (payment.Allocations.Sum(x => x.Amount) != payment.Amount)
                return ServiceResult.Fail("Tổng tiền phân bổ cho các đơn không khớp phiếu thu.");
            foreach (var allocation in payment.Allocations)
            {
                if (allocation.Document.CustomerId != payment.CustomerId ||
                    allocation.Document.Status != DocumentStatus.Completed ||
                    allocation.Document.DebtAmount <= 0 || allocation.Amount > allocation.Document.DebtAmount)
                    return ServiceResult.Fail("Công nợ của một đơn bán đã thay đổi. Vui lòng kiểm tra lại.");
            }
            foreach (var allocation in payment.Allocations)
            {
                allocation.Document.PaidAmount += allocation.Amount;
                allocation.Document.DebtAmount -= allocation.Amount;
            }
        }
        else if (payment.Document is not null)
        {
            if (payment.Document.Status != DocumentStatus.Completed ||
                payment.Document.DebtAmount <= 0 || payment.Amount > payment.Document.DebtAmount)
                return ServiceResult.Fail("Công nợ của đơn bán đã thay đổi. Vui lòng kiểm tra lại.");
            payment.Document.PaidAmount += payment.Amount;
            payment.Document.DebtAmount -= payment.Amount;
        }
        payment.Customer.Debt -= payment.Amount;
        payment.Status = PaymentStatus.Completed;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Đã hoàn thành phiếu thu và cập nhật công nợ.", payment.Id);
    }

    public async Task<ServiceResult> UpdatePaymentAsync(Payment model)
    {
        if (model.Amount <= 0) return ServiceResult.Fail("Số tiền thanh toán phải lớn hơn 0.");
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var payment = await _db.Payments
                    .Include(x => x.Customer)
                    .Include(x => x.Document)
                    .Include(x => x.Allocations).ThenInclude(x => x.Document)
                    .FirstOrDefaultAsync(x => x.Id == model.Id);
                if (payment is null) return ServiceResult.Fail("Không tìm thấy phiếu thu.");
                if (payment.Remark == "Thanh toán khi bán hàng")
                    return ServiceResult.Fail("Thanh toán tạo cùng đơn bán không thể sửa riêng. Hãy điều chỉnh trên đơn bán nếu cần.");
                if (payment.Status == PaymentStatus.Cancelled)
                    return ServiceResult.Fail("Phiếu thu đã bị hủy nên không thể sửa.");

                var wasCompleted = payment.Status == PaymentStatus.Completed;

                var customer = payment.CustomerId == model.CustomerId
                    ? payment.Customer
                    : await _db.Customers.FirstOrDefaultAsync(x => x.Id == model.CustomerId && x.IsActive);
                if (customer is null || !customer.IsActive)
                    return ServiceResult.Fail("Khách hàng không tồn tại hoặc đã ngừng sử dụng.");

                var targetAllocations = model.Allocations.Where(x => x.DocumentId > 0 && x.Amount > 0).ToList();
                if (targetAllocations.Count == 0 && model.DocumentId.HasValue)
                    targetAllocations.Add(new PaymentAllocation { DocumentId = model.DocumentId.Value, Amount = model.Amount });
                if (targetAllocations.Select(x => x.DocumentId).Distinct().Count() != targetAllocations.Count)
                    return ServiceResult.Fail("Mỗi đơn bán chỉ được chọn một lần.");
                if (targetAllocations.Count > 0 && targetAllocations.Sum(x => x.Amount) != model.Amount)
                    return ServiceResult.Fail("Tổng tiền phân bổ cho các đơn không khớp phiếu thu.");

                var availableCustomerDebt = customer.Debt +
                    (wasCompleted && payment.CustomerId == customer.Id ? payment.Amount : 0);
                if (model.Amount > availableCustomerDebt)
                    return ServiceResult.Fail("Số tiền thanh toán vượt công nợ hiện tại của khách hàng.");

                var targetDocuments = new Dictionary<long, StockDocument>();
                if (targetAllocations.Count > 0)
                {
                    var documentIds = targetAllocations.Select(x => x.DocumentId).ToList();
                    targetDocuments = await _db.StockDocuments.Where(x => documentIds.Contains(x.Id) &&
                            x.CustomerId == customer.Id && x.DocumentType == StockDocumentType.Sale &&
                            x.Status == DocumentStatus.Completed)
                        .ToDictionaryAsync(x => x.Id);
                    if (targetDocuments.Count != documentIds.Count)
                        return ServiceResult.Fail("Có đơn bán không hợp lệ với khách hàng đã chọn.");

                    var oldAppliedByDocument = payment.Allocations
                        .GroupBy(x => x.DocumentId).ToDictionary(x => x.Key, x => x.Sum(a => a.Amount));
                    if (payment.DocumentId.HasValue)
                        oldAppliedByDocument[payment.DocumentId.Value] =
                            oldAppliedByDocument.GetValueOrDefault(payment.DocumentId.Value) + payment.Amount;
                    foreach (var allocation in targetAllocations)
                    {
                        var availableDebt = targetDocuments[allocation.DocumentId].DebtAmount +
                            (wasCompleted && payment.CustomerId == customer.Id
                                ? oldAppliedByDocument.GetValueOrDefault(allocation.DocumentId)
                                : 0);
                        if (allocation.Amount > availableDebt)
                            return ServiceResult.Fail("Số tiền thu của một đơn đang vượt phần còn nợ.");
                    }
                }

                if (wasCompleted)
                {
                    payment.Customer.Debt += payment.Amount;
                    if (payment.Document is not null)
                    {
                        payment.Document.PaidAmount -= payment.Amount;
                        payment.Document.DebtAmount += payment.Amount;
                    }
                    foreach (var allocation in payment.Allocations)
                    {
                        allocation.Document.PaidAmount -= allocation.Amount;
                        allocation.Document.DebtAmount += allocation.Amount;
                    }
                }

                if (wasCompleted)
                {
                    customer.Debt -= model.Amount;
                    foreach (var allocation in targetAllocations)
                    {
                        var document = targetDocuments[allocation.DocumentId];
                        document.PaidAmount += allocation.Amount;
                        document.DebtAmount -= allocation.Amount;
                    }
                }
                else if (payment.Status == PaymentStatus.Issued)
                    payment.Status = PaymentStatus.Draft;

                _db.PaymentAllocations.RemoveRange(payment.Allocations);
                await _db.SaveChangesAsync();

                payment.CustomerId = customer.Id;
                payment.DocumentId = null;
                payment.Customer = customer;
                payment.Document = null;
                payment.Amount = model.Amount;
                payment.PaymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;
                payment.PaymentMethod = model.PaymentMethod;
                payment.Remark = CleanNullable(model.Remark);
                payment.Allocations = targetAllocations.Select(x => new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    DocumentId = x.DocumentId,
                    Amount = x.Amount
                }).ToList();

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult.Ok(wasCompleted
                    ? "Đã cập nhật phiếu thu và tính lại công nợ."
                    : "Đã cập nhật phiếu thu chờ xử lý.", payment.Id);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật phiếu thu {PaymentId}", model.Id);
            return ServiceResult.Fail("Không thể cập nhật phiếu thu. Giao dịch đã được hoàn tác.");
        }
    }

    public async Task<ServiceResult> DeletePaymentAsync(long id)
    {
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var payment = await _db.Payments.Include(x => x.Customer).Include(x => x.Document)
                    .Include(x => x.Allocations).ThenInclude(x => x.Document)
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (payment is null) return ServiceResult.Fail("Không tìm thấy phiếu thu.");
                if (payment.Remark == "Thanh toán khi bán hàng")
                    return ServiceResult.Fail("Thanh toán tạo cùng đơn bán không thể xóa riêng. Hãy hủy đơn bán nếu cần.");
                if (payment.Status == PaymentStatus.Completed)
                {
                    payment.Customer.Debt += payment.Amount;
                    if (payment.Document is not null)
                    {
                        payment.Document.PaidAmount -= payment.Amount;
                        payment.Document.DebtAmount += payment.Amount;
                    }
                    foreach (var allocation in payment.Allocations)
                    {
                        allocation.Document.PaidAmount -= allocation.Amount;
                        allocation.Document.DebtAmount += allocation.Amount;
                    }
                }
                _db.Payments.Remove(payment);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult.Ok("Đã xóa phiếu thu và hoàn tác công nợ.", id);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa phiếu thu {PaymentId}", id);
            return ServiceResult.Fail("Không thể xóa phiếu thu. Giao dịch đã được hoàn tác.");
        }
    }

    public async Task<ServiceResult> CancelPaymentAsync(long id)
    {
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var payment = await _db.Payments.Include(x => x.Customer).Include(x => x.Document)
                    .Include(x => x.Allocations).ThenInclude(x => x.Document)
                    .FirstOrDefaultAsync(x => x.Id == id);
                if (payment is null) return ServiceResult.Fail("Không tìm thấy phiếu thu.");
                if (payment.Remark == "Thanh toán khi bán hàng")
                    return ServiceResult.Fail("Khoản thanh toán tự động không thể hủy riêng.");
                if (payment.Status == PaymentStatus.Cancelled)
                    return ServiceResult.Fail("Phiếu thu đã bị hủy.");
                if (payment.Status == PaymentStatus.Completed)
                {
                    payment.Customer.Debt += payment.Amount;
                    if (payment.Document is not null)
                    {
                        payment.Document.PaidAmount -= payment.Amount;
                        payment.Document.DebtAmount += payment.Amount;
                    }
                    foreach (var allocation in payment.Allocations)
                    {
                        allocation.Document.PaidAmount -= allocation.Amount;
                        allocation.Document.DebtAmount += allocation.Amount;
                    }
                }
                payment.Status = PaymentStatus.Cancelled;
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return ServiceResult.Ok("Đã hủy phiếu thu và hoàn tác công nợ nếu có.", payment.Id);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi hủy phiếu thu {PaymentId}", id);
            return ServiceResult.Fail("Không thể hủy phiếu thu. Giao dịch đã được hoàn tác.");
        }
    }

    public Task<List<StockDocument>> GetOutstandingSalesAsync(
        int customerId, IReadOnlyCollection<long>? includeDocumentIds = null)
    {
        var includedIds = includeDocumentIds?.Distinct().ToList() ?? [];
        return _db.StockDocuments.AsNoTracking().Where(x => x.CustomerId == customerId &&
                x.DocumentType == StockDocumentType.Sale && x.Status == DocumentStatus.Completed &&
                (x.DebtAmount > 0 || includedIds.Contains(x.Id)))
            .OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.Id).ToListAsync();
    }

    public async Task<decimal> GetCustomerOutstandingDebtAsync(int customerId)
    {
        var customerDebt = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => x.Debt)
            .FirstOrDefaultAsync();
        var trackedDebt = await _db.StockDocuments.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.DocumentType == StockDocumentType.Sale &&
                        x.Status == DocumentStatus.Completed && x.DebtAmount > 0)
            .SumAsync(x => (decimal?)x.DebtAmount) ?? 0;
        return Math.Min(customerDebt, trackedDebt);
    }

    #endregion

    #region Dashboard and reports

    public async Task<DashboardData> GetDashboardAsync(DateTime? fromDate, DateTime? toDate, decimal threshold = 5)
    {
        var from = fromDate?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = toDate?.Date.AddDays(1) ?? DateTime.Today.AddDays(1);
        var documents = _db.StockDocuments.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Completed && x.DocumentDate >= from && x.DocumentDate < to);
        var totalQuantity = await _db.WarehouseStocks.SumAsync(x => (decimal?)x.Quantity) ?? 0;
        var inventoryValue = await _db.WarehouseStocks.SumAsync(x => (decimal?)(x.Quantity * x.Item.CostPrice)) ?? 0;
        var imports = await documents.Where(x => x.DocumentType == StockDocumentType.Import)
            .SelectMany(x => x.Details).SumAsync(x => (decimal?)x.Quantity) ?? 0;
        var exports = await documents.Where(x => x.DocumentType == StockDocumentType.Export)
            .SelectMany(x => x.Details).SumAsync(x => (decimal?)x.Quantity) ?? 0;
        var revenue = await documents.Where(x => x.DocumentType == StockDocumentType.Sale)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
        var collected = await _db.Payments.AsNoTracking().Where(x =>
                x.Status == PaymentStatus.Completed && x.PaymentDate >= from && x.PaymentDate < to)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var debt = await _db.Customers.AsNoTracking().SumAsync(x => (decimal?)x.Debt) ?? 0;
        var lowStocks = await GetLowStockAsync(threshold);
        var topSelling = await documents.Where(x => x.DocumentType == StockDocumentType.Sale)
            .SelectMany(x => x.Details)
            .GroupBy(x => new { x.ItemId, x.Item.Code, x.Item.Product.Name })
            .Select(x => new TopSellingRow
            {
                ItemId = x.Key.ItemId,
                ItemCode = x.Key.Code,
                ProductName = x.Key.Name,
                Quantity = x.Sum(y => y.Quantity),
                Revenue = x.Sum(y => y.Amount)
            }).OrderByDescending(x => x.Quantity).Take(10).ToListAsync();
        var topSellingItemIds = topSelling.Select(x => x.ItemId).ToList();
        var topSellingAttributes = (await _db.Items.AsNoTracking()
                .Where(x => topSellingItemIds.Contains(x.Id))
                .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
                .ToListAsync())
            .ToDictionary(x => x.Id, x => FormatAttributes(x.ItemAttributes));
        foreach (var row in topSelling)
            row.Attributes = topSellingAttributes.GetValueOrDefault(row.ItemId) ?? string.Empty;
        var recent = await _db.StockDocuments.AsNoTracking().Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync();
        return new DashboardData
        {
            TotalQuantity = totalQuantity,
            InventoryValue = inventoryValue,
            ImportQuantity = imports,
            ExportQuantity = exports,
            SalesRevenue = revenue,
            CollectedAmount = collected,
            CustomerDebt = debt,
            LowStockCount = lowStocks.Count,
            LowStocks = lowStocks,
            TopSelling = topSelling,
            RecentDocuments = recent
        };
    }

    public Task<StockMatrix> GetInventoryReportAsync(int? productId, string? keyword, int? warehouseId) =>
        GetStockMatrixAsync(productId, keyword, warehouseId);

    public async Task<List<StockMovementRow>> GetStockMovementReportAsync(
        DateTime fromDate, DateTime toDate, int? warehouseId)
    {
        var from = fromDate.Date;
        var end = toDate.Date.AddDays(1);
        var currentStocks = await _db.WarehouseStocks.AsNoTracking()
            .Where(x => !warehouseId.HasValue || x.WarehouseId == warehouseId.Value)
            .GroupBy(x => new
            {
                x.ItemId,
                x.Item.ProductId,
                x.Item.Code,
                ProductName = x.Item.Product.Name
            })
            .Select(x => new
            {
                x.Key.ItemId,
                x.Key.ProductId,
                x.Key.Code,
                x.Key.ProductName,
                Quantity = x.Sum(y => y.Quantity)
            })
            .ToListAsync();
        var documents = await _db.StockDocuments.AsNoTracking().Where(x =>
                x.Status == DocumentStatus.Completed && x.DocumentDate >= from)
            .Include(x => x.Details).ToListAsync();
        var itemIds = currentStocks.Select(x => x.ItemId)
            .Concat(documents.SelectMany(x => x.Details).Select(x => x.ItemId)).Distinct().ToList();
        var itemNames = (await _db.Items.AsNoTracking().Where(x => itemIds.Contains(x.Id))
                .Include(x => x.Product)
                .Include(x => x.ItemAttributes).ThenInclude(x => x.AttributeValue).ThenInclude(x => x.Attribute)
                .ToListAsync())
            .ToDictionary(x => x.Id);
        var rows = new List<StockMovementRow>();
        foreach (var itemId in itemIds)
        {
            var current = currentStocks.FirstOrDefault(x => x.ItemId == itemId)?.Quantity ?? 0;
            var periodDocs = documents.Where(x => x.DocumentDate < end);
            var afterDocs = documents.Where(x => x.DocumentDate >= end);
            var periodEffect = periodDocs.Sum(x => DocumentEffect(x, itemId, warehouseId));
            var afterEffect = afterDocs.Sum(x => DocumentEffect(x, itemId, warehouseId));
            var closing = current - afterEffect;
            var item = itemNames[itemId];
            var row = new StockMovementRow
            {
                ItemId = itemId,
                ProductId = item.ProductId,
                ItemCode = item.Code,
                ProductName = item.Product.Name,
                Attributes = FormatAttributes(item.ItemAttributes),
                OpeningQuantity = closing - periodEffect,
                ClosingQuantity = closing
            };
            foreach (var document in periodDocs)
            {
                var quantity = document.Details.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
                if (quantity == 0) continue;
                switch (document.DocumentType)
                {
                    case StockDocumentType.Import when AppliesToWarehouse(document.ToWarehouseId, warehouseId):
                        row.ImportQuantity += quantity;
                        break;
                    case StockDocumentType.Export when AppliesToWarehouse(document.FromWarehouseId, warehouseId):
                        row.ExportQuantity += quantity;
                        break;
                    case StockDocumentType.Sale when AppliesToWarehouse(document.FromWarehouseId, warehouseId):
                        row.SaleQuantity += quantity;
                        break;
                    case StockDocumentType.Return when AppliesToWarehouse(document.ToWarehouseId, warehouseId):
                        row.ReturnQuantity += quantity;
                        break;
                    case StockDocumentType.Transfer:
                        if (warehouseId.HasValue && document.FromWarehouseId == warehouseId) row.TransferOut += quantity;
                        if (warehouseId.HasValue && document.ToWarehouseId == warehouseId) row.TransferIn += quantity;
                        break;
                    case StockDocumentType.Adjust when AppliesToWarehouse(document.ToWarehouseId, warehouseId):
                        row.AdjustQuantity += quantity;
                        break;
                }
            }
            rows.Add(row);
        }
        return rows
            .OrderByDescending(x => x.ClosingQuantity > 0)
            .ThenByDescending(x => x.ProductId)
            .ThenByDescending(x => x.ItemId)
            .ThenBy(x => x.ItemCode)
            .ToList();
    }

    public Task<PagedResult<StockDocument>> GetSalesReportAsync(
        DateTime? fromDate, DateTime? toDate, string? keyword, int page = 1, int pageSize = 50) =>
        GetSalesHistoryAsync(fromDate, toDate, keyword, page, pageSize);

    public async Task<(int OrderCount, decimal Quantity, decimal TotalAmount, decimal PaidAmount, decimal DebtAmount)>
        GetSalesReportSummaryAsync(DateTime? fromDate, DateTime? toDate, string? keyword)
    {
        var query = ApplySalesHistoryFilters(
            _db.StockDocuments.AsNoTracking(), fromDate, toDate, keyword);
        var summary = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                OrderCount = group.Count(),
                Quantity = group.SelectMany(x => x.Details).Sum(x => x.Quantity),
                TotalAmount = group.Sum(x => x.TotalAmount),
                PaidAmount = group.Sum(x => x.PaidAmount),
                DebtAmount = group.Sum(x => x.DebtAmount)
            })
            .SingleOrDefaultAsync();

        return summary is null
            ? (0, 0, 0, 0, 0)
            : (summary.OrderCount, summary.Quantity, summary.TotalAmount, summary.PaidAmount, summary.DebtAmount);
    }

    public async Task<List<DebtReportRow>> GetDebtReportAsync(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate?.Date ?? DateTime.MinValue;
        var to = toDate?.Date.AddDays(1) ?? DateTime.MaxValue;
        return await _db.Customers.AsNoTracking().Where(x => x.IsActive || x.Debt != 0)
            .Select(x => new DebtReportRow
            {
                CustomerId = x.Id,
            CustomerCode = x.Code ?? string.Empty,
                CustomerName = x.Name,
                CurrentDebt = x.Debt,
                OutstandingSaleCount = x.Documents.Count(d => d.DocumentType == StockDocumentType.Sale &&
                    d.Status == DocumentStatus.Completed && d.DebtAmount > 0),
                OutstandingSaleAmount = x.Documents.Where(d => d.DocumentType == StockDocumentType.Sale &&
                    d.Status == DocumentStatus.Completed && d.DebtAmount > 0).Sum(d => d.DebtAmount),
                PaymentInPeriod = x.Payments.Where(p => p.Status == PaymentStatus.Completed &&
                    p.PaymentDate >= from && p.PaymentDate < to).Sum(p => p.Amount)
            }).OrderByDescending(x => x.CurrentDebt).ThenBy(x => x.CustomerName).ToListAsync();
    }

    #endregion

    #region Audit logs

    public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(
        string? userName,
        string? action,
        string? entityName,
        DateTime? fromDate,
        DateTime? toDate,
        string? keyword,
        int page = 1,
        int pageSize = 50,
        string? sort = null,
        string? direction = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        userName = CleanNullable(userName);
        action = CleanNullable(action);
        entityName = CleanNullable(entityName);
        keyword = CleanNullable(keyword);
        if (userName is not null) query = query.Where(x => x.UserName == userName);
        if (action is not null) query = query.Where(x => x.Action == action);
        if (entityName is not null) query = query.Where(x => x.EntityName == entityName);
        if (fromDate.HasValue) query = query.Where(x => x.OccurredAt >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.OccurredAt < end);
        }
        if (keyword is not null)
        {
            query = query.Where(x => x.Description.Contains(keyword) ||
                                     (x.EntityId != null && x.EntityId.Contains(keyword)) ||
                                     (x.RequestPath != null && x.RequestPath.Contains(keyword)) ||
                                     (x.IpAddress != null && x.IpAddress.Contains(keyword)));
        }

        var total = await query.CountAsync();
        var descending = IsDescending(direction);
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("date", false) => query.OrderBy(x => x.OccurredAt),
            ("user", true) => query.OrderByDescending(x => x.UserName),
            ("user", false) => query.OrderBy(x => x.UserName),
            ("action", true) => query.OrderByDescending(x => x.Action),
            ("action", false) => query.OrderBy(x => x.Action),
            ("entity", true) => query.OrderByDescending(x => x.EntityName),
            ("entity", false) => query.OrderBy(x => x.EntityName),
            ("description", true) => query.OrderByDescending(x => x.Description),
            ("description", false) => query.OrderBy(x => x.Description),
            ("path", true) => query.OrderByDescending(x => x.RequestPath),
            ("path", false) => query.OrderBy(x => x.RequestPath),
            _ => query.OrderByDescending(x => x.OccurredAt)
        };
        var items = await ordered.ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public Task<List<string>> GetAuditLogUsersAsync() =>
        _db.AuditLogs.AsNoTracking()
            .Select(x => x.UserName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    public Task<List<string>> GetAuditLogEntityNamesAsync() =>
        _db.AuditLogs.AsNoTracking()
            .Select(x => x.EntityName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    public async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        object? changes = null,
        string? userName = null)
    {
        var changesJson = changes is null ? null : JsonSerializer.Serialize(changes);
        _db.AuditLogs.Add(AuditLogTrail.CreateManual(
            action,
            entityName,
            entityId,
            description,
            changesJson,
            _auditUserProvider,
            userName));
        await _db.SaveChangesAsync();
    }

    #endregion

    #region Private helpers

    private async Task<ServiceResult> SaveAsTypeAsync(DocumentInput input, StockDocumentType type)
    {
        input.DocumentType = type;
        return await SaveAndCompleteDocumentAsync(input);
    }

    private async Task<ServiceResult> ValidateDocumentInputAsync(DocumentInput input)
    {
        var submittedDetails = (input.Details ?? []).Where(x => x.ItemId > 0).ToList();
        if (submittedDetails.Any(x => x.Quantity != decimal.Truncate(x.Quantity)))
            return ServiceResult.Fail("Số lượng phải là số nguyên.");
        var details = NormalizeDetails(submittedDetails);
        if (details.Count == 0) return ServiceResult.Fail("Chứng từ phải có ít nhất một dòng chi tiết.");
        if (details.Any(x => x.Price < 0)) return ServiceResult.Fail("Đơn giá không được âm.");
        if (input.DocumentType == StockDocumentType.Adjust)
        {
            if (details.Any(x => x.Quantity == 0)) return ServiceResult.Fail("Số lượng điều chỉnh không được bằng 0.");
        }
        else if (details.Any(x => x.Quantity <= 0))
            return ServiceResult.Fail("Số lượng phải lớn hơn 0.");
        if (input.PaidAmount < 0) return ServiceResult.Fail("Số tiền đã trả không được âm.");
        if (input.DiscountAmount < 0) return ServiceResult.Fail("Chiết khấu không được âm.");
        if (input.DocumentType != StockDocumentType.Sale && input.DiscountAmount != 0)
            return ServiceResult.Fail("Chỉ đơn bán hàng mới được nhập chiết khấu.");
        var itemIds = details.Select(x => x.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(x => itemIds.Contains(x.Id) && x.IsActive) != itemIds.Count)
            return ServiceResult.Fail("Có phân loại sản phẩm không tồn tại hoặc đã ngừng sử dụng.");

        switch (input.DocumentType)
        {
            case StockDocumentType.Import:
                if (!await ValidWarehouseAsync(input.ToWarehouseId)) return ServiceResult.Fail("Kho nhập là bắt buộc.");
                if (input.FromWarehouseId.HasValue) return ServiceResult.Fail("Phiếu nhập không được chọn kho nguồn.");
                break;
            case StockDocumentType.Export:
            case StockDocumentType.Sale:
                if (!await ValidWarehouseAsync(input.FromWarehouseId)) return ServiceResult.Fail("Kho xuất là bắt buộc.");
                if (input.ToWarehouseId.HasValue) return ServiceResult.Fail("Phiếu xuất/bán không được chọn kho đích.");
                break;
            case StockDocumentType.Transfer:
                if (!await ValidWarehouseAsync(input.FromWarehouseId) || !await ValidWarehouseAsync(input.ToWarehouseId))
                    return ServiceResult.Fail("Kho nguồn và kho đích là bắt buộc.");
                if (input.FromWarehouseId == input.ToWarehouseId)
                    return ServiceResult.Fail("Kho nguồn và kho đích phải khác nhau.");
                break;
            case StockDocumentType.Return:
            case StockDocumentType.Adjust:
                if (!await ValidWarehouseAsync(input.ToWarehouseId)) return ServiceResult.Fail("Kho nhận/điều chỉnh là bắt buộc.");
                break;
        }
        if (input.DocumentType is StockDocumentType.Export or StockDocumentType.Transfer &&
            input.FromWarehouseId.HasValue)
        {
            var availableItems = await _db.Items.AsNoTracking()
                .Where(x => itemIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    ProductName = x.Product.Name,
                    Stock = x.WarehouseStocks
                        .Where(stock => stock.WarehouseId == input.FromWarehouseId.Value)
                        .Sum(stock => (decimal?)stock.Quantity) ?? 0
                })
                .ToListAsync();

            foreach (var detail in details)
            {
                var available = availableItems.First(x => x.Id == detail.ItemId);
                if (available.Stock < detail.Quantity)
                {
                    return ServiceResult.Fail(
                        $"Sản phẩm {available.ProductName} không đủ tồn trong kho đã chọn " +
                        $"(còn {available.Stock:N0}, cần {detail.Quantity:N0}).");
                }
            }
        }
        if (input.DocumentType is StockDocumentType.Sale or StockDocumentType.Return)
        {
            if (!input.CustomerId.HasValue || !await _db.Customers.AnyAsync(x => x.Id == input.CustomerId && x.IsActive))
                return ServiceResult.Fail("Khách hàng là bắt buộc.");
        }
        return ServiceResult.Ok("Dữ liệu hợp lệ.");
    }

    private async Task<ServiceResult> ApplyDocumentStockAsync(StockDocument document, bool reverse)
    {
        foreach (var detail in document.Details)
        {
            switch (document.DocumentType)
            {
                case StockDocumentType.Import:
                    if (reverse)
                    {
                        var result = await DecreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                        if (!result.Success) return ServiceResult.Fail("Không thể hủy nhập: " + result.Message);
                    }
                    else await IncreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    break;
                case StockDocumentType.Export:
                    if (reverse) await IncreaseStockAsync(document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    else
                    {
                        var result = await DecreaseStockAsync(document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                        if (!result.Success) return result;
                    }
                    break;
                case StockDocumentType.Sale:
                    if (reverse) await IncreaseStockAsync(document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    else await DecreaseStockAllowNegativeAsync(
                        document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    break;
                case StockDocumentType.Transfer:
                    if (reverse)
                    {
                        var result = await DecreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                        if (!result.Success) return ServiceResult.Fail("Không thể hủy chuyển kho: " + result.Message);
                        await IncreaseStockAsync(document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    }
                    else
                    {
                        var result = await DecreaseStockAsync(document.FromWarehouseId!.Value, detail.ItemId, detail.Quantity);
                        if (!result.Success) return result;
                        await IncreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    }
                    break;
                case StockDocumentType.Return:
                    if (reverse)
                    {
                        var result = await DecreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                        if (!result.Success) return ServiceResult.Fail("Không thể hủy trả hàng: " + result.Message);
                    }
                    else await IncreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, detail.Quantity);
                    break;
                case StockDocumentType.Adjust:
                    var adjustment = reverse ? -detail.Quantity : detail.Quantity;
                    if (adjustment > 0) await IncreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, adjustment);
                    else
                    {
                        var result = await DecreaseStockAsync(document.ToWarehouseId!.Value, detail.ItemId, -adjustment);
                        if (!result.Success) return ServiceResult.Fail("Không thể áp dụng điều chỉnh: " + result.Message);
                    }
                    break;
            }
        }
        return ServiceResult.Ok("Đã cập nhật tồn.");
    }

    private async Task IncreaseStockAsync(int warehouseId, int itemId, decimal quantity)
    {
        var stock = await _db.WarehouseStocks.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ItemId == itemId);
        if (stock is null)
        {
            _db.WarehouseStocks.Add(new WarehouseStock
                { WarehouseId = warehouseId, ItemId = itemId, Quantity = quantity });
        }
        else stock.Quantity += quantity;
    }

    private async Task<ServiceResult> DecreaseStockAsync(int warehouseId, int itemId, decimal quantity)
    {
        var stock = await _db.WarehouseStocks.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ItemId == itemId);
        if (stock is null || stock.Quantity < quantity)
        {
            var productName = await _db.Items.Where(x => x.Id == itemId)
                .Select(x => x.Product.Name).FirstOrDefaultAsync() ?? "đã chọn";
            return ServiceResult.Fail(
                $"Sản phẩm {productName} không đủ tồn (còn {stock?.Quantity ?? 0:N0}, cần {quantity:N0}).");
        }
        stock.Quantity -= quantity;
        return ServiceResult.Ok("Đủ tồn.");
    }

    private async Task DecreaseStockAllowNegativeAsync(int warehouseId, int itemId, decimal quantity)
    {
        var stock = await _db.WarehouseStocks.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId && x.ItemId == itemId);
        if (stock is null)
        {
            _db.WarehouseStocks.Add(new WarehouseStock
            {
                WarehouseId = warehouseId,
                ItemId = itemId,
                Quantity = -quantity
            });
        }
        else
        {
            stock.Quantity -= quantity;
        }
    }

    private Task<bool> ValidWarehouseAsync(int? id) =>
        id.HasValue ? _db.Warehouses.AnyAsync(x => x.Id == id && x.IsActive) : Task.FromResult(false);

    private async Task<string> GenerateDocumentNoAsync(StockDocumentType type)
    {
        var prefix = type switch
        {
            StockDocumentType.Import => "IMP",
            StockDocumentType.Export => "EXP",
            StockDocumentType.Transfer => "TRF",
            StockDocumentType.Sale => "SAL",
            StockDocumentType.Return => "RET",
            _ => "ADJ"
        };
        var periodPrefix = $"{prefix}-{DateTime.Now:yyyyMM}-";
        var last = await _db.StockDocuments.AsNoTracking().Where(x => x.DocumentNo.StartsWith(periodPrefix))
            .OrderByDescending(x => x.DocumentNo).Select(x => x.DocumentNo).FirstOrDefaultAsync();
        var next = last is not null && int.TryParse(last[periodPrefix.Length..], out var sequence) ? sequence + 1 : 1;
        return $"{periodPrefix}{next:0000}";
    }

    private static List<DocumentDetailInput> NormalizeDetails(IEnumerable<DocumentDetailInput>? details) =>
        (details ?? []).Where(x => x.ItemId > 0)
            .GroupBy(x => x.ItemId)
            .Select(x => new DocumentDetailInput
            {
                ItemId = x.Key,
                Quantity = x.Sum(y => y.Quantity),
                Price = x.Last().Price
            }).ToList();

    private static ItemRow ToItemRow(Item item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductName = item.Product.Name,
        Code = item.Code,
        Barcode = item.Barcode,
        CostPrice = item.CostPrice,
        SalePrice = item.SalePrice,
        Attributes = FormatAttributes(item.ItemAttributes),
        TotalStock = item.WarehouseStocks.Sum(x => x.Quantity),
        IsActive = item.IsActive
    };

    public static string FormatAttributes(IEnumerable<ItemAttribute> attributes) => string.Join(", ",
        attributes.OrderBy(x => x.AttributeValue.Attribute.Name).ThenBy(x => x.AttributeValue.SortOrder)
            .Select(x => $"{x.AttributeValue.Attribute.Name}: {x.AttributeValue.Value}"));

    private static decimal DocumentEffect(StockDocument document, int itemId, int? warehouseId)
    {
        var quantity = document.Details.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
        if (quantity == 0) return 0;
        return document.DocumentType switch
        {
            StockDocumentType.Import when AppliesToWarehouse(document.ToWarehouseId, warehouseId) => quantity,
            StockDocumentType.Export when AppliesToWarehouse(document.FromWarehouseId, warehouseId) => -quantity,
            StockDocumentType.Sale when AppliesToWarehouse(document.FromWarehouseId, warehouseId) => -quantity,
            StockDocumentType.Return when AppliesToWarehouse(document.ToWarehouseId, warehouseId) => quantity,
            StockDocumentType.Adjust when AppliesToWarehouse(document.ToWarehouseId, warehouseId) => quantity,
            StockDocumentType.Transfer when warehouseId.HasValue && document.FromWarehouseId == warehouseId => -quantity,
            StockDocumentType.Transfer when warehouseId.HasValue && document.ToWarehouseId == warehouseId => quantity,
            _ => 0
        };
    }

    private static bool AppliesToWarehouse(int? documentWarehouseId, int? filterWarehouseId) =>
        !filterWarehouseId.HasValue || documentWarehouseId == filterWarehouseId;

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int total, int page, int pageSize) =>
        new() { Items = items, TotalItems = total, Page = page, PageSize = pageSize };

    private static bool IsDescending(string? direction) =>
        string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCode(string? value) => Clean(value).ToUpperInvariant();
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static string? CleanNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void NormalizeProductContent(Product model)
    {
        model.NameEn = CleanNullable(model.NameEn);
        model.NameDe = CleanNullable(model.NameDe);
        model.ShortDescriptionEn = CleanNullable(model.ShortDescriptionEn);
        model.ShortDescriptionDe = CleanNullable(model.ShortDescriptionDe);
        model.DetailContentEn = CleanNullable(model.DetailContentEn);
        model.DetailContentDe = CleanNullable(model.DetailContentDe);
        model.Description = model.ShortDescription;
        model.SeoTitle = model.Name;
        model.SeoDescription = Limit(model.ShortDescription ?? PlainText(model.DetailContent), 320);
        model.SeoKeywords = BuildProductKeywords(model.Name, model.Category, model.ShortDescription);
        model.SeoTitleEn = model.NameEn;
        model.SeoDescriptionEn = Limit(model.ShortDescriptionEn ?? PlainText(model.DetailContentEn), 320);
        model.SeoKeywordsEn = BuildProductKeywords(model.NameEn, model.ShortDescriptionEn);
        model.SeoTitleDe = model.NameDe;
        model.SeoDescriptionDe = Limit(model.ShortDescriptionDe ?? PlainText(model.DetailContentDe), 320);
        model.SeoKeywordsDe = BuildProductKeywords(model.NameDe, model.ShortDescriptionDe);
    }

    private static string? PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text[..Math.Min(320, text.Length)];
    }

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(maxLength, value.Length)];

    private static string? BuildProductKeywords(params string?[] values)
    {
        var keywords = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(new[] { ',', ';', '|', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim().Trim('.', ':', '-', '_'))
            .Where(value => value.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        return keywords.Length == 0 ? null : string.Join(", ", keywords);
    }

    #endregion
}
