using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WareHouse.Data;
using WareHouse.Web.Models;

namespace WareHouse.Web.Services;

public sealed class ProductImageService
{
    private readonly WareHouseDbContext _db;
    private readonly ProductImageStorageOptions _options;
    private readonly ILogger<ProductImageService> _logger;
    private readonly string _rootPath;

    public ProductImageService(
        WareHouseDbContext db,
        IOptions<ProductImageStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<ProductImageService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        _rootPath = ResolveRootPath(_options.RootPath, environment.ContentRootPath);
    }

    public static string ResolveRootPath(string? configuredPath, string contentRootPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "data/images"
            : configuredPath.Trim();
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path));
    }

    public async Task<ServiceResult> ValidateAsync(
        int productId,
        IReadOnlyCollection<int> removedImageIds,
        IReadOnlyList<IFormFile> newImages)
    {
        var currentImages = productId > 0
            ? await _db.ProductImages.AsNoTracking()
                .Where(x => x.ProductId == productId)
                .Select(x => x.Id)
                .ToListAsync()
            : [];

        if (removedImageIds.Any(x => !currentImages.Contains(x)))
            return ServiceResult.Fail("Danh sách ảnh cần xóa không hợp lệ.");

        var remainingCount = currentImages.Count - removedImageIds.Distinct().Count();
        if (remainingCount + newImages.Count > Math.Max(1, _options.MaxFilesPerProduct))
            return ServiceResult.Fail($"Mỗi sản phẩm chỉ được lưu tối đa {_options.MaxFilesPerProduct} ảnh.");

        var maxFileSize = Math.Max(1, _options.MaxFileSizeMb) * 1024L * 1024L;
        var maxTotalSize = Math.Max(1, _options.MaxTotalSizeMb) * 1024L * 1024L;
        if (newImages.Sum(x => x.Length) > maxTotalSize)
            return ServiceResult.Fail($"Tổng dung lượng ảnh tải lên không được vượt quá {_options.MaxTotalSizeMb} MB.");

        foreach (var file in newImages)
        {
            if (file.Length <= 0)
                return ServiceResult.Fail($"Ảnh “{SafeFileName(file.FileName)}” không có dữ liệu.");
            if (file.Length > maxFileSize)
                return ServiceResult.Fail($"Ảnh “{SafeFileName(file.FileName)}” vượt quá {_options.MaxFileSizeMb} MB.");
            if (await DetectImageTypeAsync(file) is null)
                return ServiceResult.Fail($"Ảnh “{SafeFileName(file.FileName)}” không đúng định dạng JPG, PNG hoặc WebP.");
        }

        return ServiceResult.Ok("Ảnh hợp lệ.");
    }

    public async Task<ServiceResult> ApplyAsync(
        int productId,
        IReadOnlyCollection<int> removedImageIds,
        IReadOnlyList<IFormFile> newImages,
        string? imageOrder,
        string? primaryImageKey,
        string? imageAssignments)
    {
        var validation = await ValidateAsync(productId, removedImageIds, newImages);
        if (!validation.Success) return validation;

        var existingImages = await _db.ProductImages
            .Include(x => x.ItemAssignments)
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();
        var removedSet = removedImageIds.ToHashSet();
        var assignments = ParseAssignments(imageAssignments);
        var validItemIds = await _db.Items.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Select(x => x.Id)
            .ToHashSetAsync();
        var removedImages = existingImages.Where(x => removedSet.Contains(x.Id)).ToList();
        var keptImages = existingImages.Where(x => !removedSet.Contains(x.Id)).ToList();
        var savedPaths = new List<string>();
        var addedImages = new List<ProductImage>();

        try
        {
            for (var index = 0; index < newImages.Count; index++)
            {
                var file = newImages[index];
                var type = await DetectImageTypeAsync(file)
                    ?? throw new InvalidDataException("Tệp ảnh không hợp lệ.");
                var relativePath = await SaveFileAsync(productId, file, type.Extension);
                savedPaths.Add(relativePath);
                addedImages.Add(new ProductImage
                {
                    ProductId = productId,
                    RelativePath = relativePath,
                    OriginalFileName = SafeFileName(file.FileName),
                    ContentType = type.ContentType,
                    FileSize = file.Length
                });
            }

            _db.ProductImages.RemoveRange(removedImages);
            _db.ProductImages.AddRange(addedImages);

            var keyedImages = new Dictionary<string, ProductImage>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in keptImages) keyedImages[$"existing-{image.Id}"] = image;
            for (var index = 0; index < addedImages.Count; index++) keyedImages[$"new-{index}"] = addedImages[index];

            foreach (var pair in keyedImages)
            {
                var image = pair.Value;
                var requestedItemIds = assignments.GetValueOrDefault(pair.Key, [])
                    .Where(validItemIds.Contains)
                    .Distinct()
                    .ToHashSet();

                var removedAssignments = image.ItemAssignments
                    .Where(x => !requestedItemIds.Contains(x.ItemId))
                    .ToList();
                if (removedAssignments.Count > 0)
                    _db.ProductImageItems.RemoveRange(removedAssignments);

                var existingItemIds = image.ItemAssignments
                    .Where(x => !removedAssignments.Contains(x))
                    .Select(x => x.ItemId)
                    .ToHashSet();
                foreach (var itemId in requestedItemIds.Except(existingItemIds))
                {
                    image.ItemAssignments.Add(new ProductImageItem
                    {
                        ProductImage = image,
                        ItemId = itemId
                    });
                }

                image.ColorValueId = null;
                image.ItemId = null;
            }

            var orderedImages = new List<ProductImage>();
            foreach (var key in ParseOrder(imageOrder))
            {
                if (keyedImages.Remove(key, out var image)) orderedImages.Add(image);
            }
            orderedImages.AddRange(keyedImages.Values
                .OrderBy(x => x.Id == 0 ? int.MaxValue : x.SortOrder)
                .ThenBy(x => x.Id));

            for (var index = 0; index < orderedImages.Count; index++)
            {
                orderedImages[index].SortOrder = index;
                orderedImages[index].IsPrimary = false;
            }

            var primary = orderedImages.FirstOrDefault(x =>
                GetImageKey(x, addedImages).Equals(primaryImageKey, StringComparison.OrdinalIgnoreCase))
                ?? orderedImages.FirstOrDefault();
            if (primary is not null) primary.IsPrimary = true;

            if (removedImages.Count > 0 || addedImages.Count > 0 || orderedImages.Count > 0)
            {
                var product = await _db.Products.FindAsync(productId);
                if (product is not null)
                    _db.Entry(product).Property(x => x.UpdatedAt).IsModified = true;
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException or InvalidDataException)
        {
            _logger.LogError(ex, "Không thể lưu ảnh cho sản phẩm {ProductId}", productId);
            await DeletePhysicalFilesAsync(savedPaths);
            return ServiceResult.Fail("Không thể lưu ảnh sản phẩm. Vui lòng kiểm tra quyền ghi thư mục ảnh và thử lại.");
        }

        await DeletePhysicalFilesAsync(removedImages.Select(x => x.RelativePath));
        return ServiceResult.Ok("Đã lưu thư viện ảnh sản phẩm.");
    }

    public Task<List<string>> GetRelativePathsAsync(int productId) =>
        _db.ProductImages.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Select(x => x.RelativePath)
            .ToListAsync();

    public async Task<List<ProductImage>> GetImagesAsync(int productId) =>
        await _db.ProductImages.AsNoTracking()
            .Include(x => x.ItemAssignments)
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

    public async Task DeletePhysicalFilesAsync(IEnumerable<string> relativePaths)
    {
        foreach (var relativePath in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var fullPath = GetSafeFullPath(relativePath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) &&
                    !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _logger.LogWarning(ex, "Không thể xóa file ảnh {RelativePath}", relativePath);
            }
        }

        await Task.CompletedTask;
    }

    private async Task<string> SaveFileAsync(int productId, IFormFile file, string extension)
    {
        var productFolder = Path.Combine(_rootPath, productId.ToString());
        Directory.CreateDirectory(productFolder);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(productFolder, storedName);
        await using var source = file.OpenReadStream();
        await using var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await source.CopyToAsync(target);
        return $"{productId}/{storedName}";
    }

    private string GetSafeFullPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Đường dẫn ảnh không hợp lệ.", nameof(relativePath));
        return fullPath;
    }

    private static IEnumerable<string> ParseOrder(string? imageOrder) =>
        (imageOrder ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.StartsWith("existing-", StringComparison.OrdinalIgnoreCase) ||
                        x.StartsWith("new-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyCollection<int>> ParseAssignments(string? value)
    {
        var assignments = new Dictionary<string, IReadOnlyCollection<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (value ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0) continue;
            var key = entry[..separator].Trim();
            if (key.StartsWith("existing-", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("new-", StringComparison.OrdinalIgnoreCase))
            {
                assignments[key] = entry[(separator + 1)..]
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => int.TryParse(x, out var itemId) ? itemId : 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();
            }
        }
        return assignments;
    }

    private static string GetImageKey(ProductImage image, IReadOnlyList<ProductImage> addedImages)
    {
        var newIndex = -1;
        for (var index = 0; index < addedImages.Count; index++)
        {
            if (ReferenceEquals(addedImages[index], image))
            {
                newIndex = index;
                break;
            }
        }
        return newIndex >= 0 ? $"new-{newIndex}" : $"existing-{image.Id}";
    }

    private static async Task<DetectedImageType?> DetectImageTypeAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return new(".jpg", "image/jpeg");
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return new(".png", "image/png");
        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return new(".webp", "image/webp");
        return null;
    }

    private static string SafeFileName(string? fileName)
    {
        var safe = Path.GetFileName(fileName ?? "image").Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "image";
        return safe.Length <= 255 ? safe : safe[..255];
    }

    private sealed record DetectedImageType(string Extension, string ContentType);
}
