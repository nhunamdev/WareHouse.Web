using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using WareHouse.Data;
using WareHouse.Web.Models;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Services;

public sealed class StoreBannerService
{
    private const int BannerWidth = 1600;
    private const int BannerHeight = 900;
    private const long MaxBannerPixels = 40_000_000;

    private readonly WareHouseDbContext _db;
    private readonly ProductImageStorageOptions _options;
    private readonly ILogger<StoreBannerService> _logger;
    private readonly string _rootPath;

    public StoreBannerService(
        WareHouseDbContext db,
        IOptions<ProductImageStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<StoreBannerService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        _rootPath = ProductImageService.ResolveRootPath(_options.RootPath, environment.ContentRootPath);
    }

    public string ImageUrl(string? relativePath) => string.IsNullOrWhiteSpace(relativePath)
        ? string.Empty
        : $"{_options.RequestPath.TrimEnd('/')}/{string.Join('/', relativePath.Split('/', '\\').Select(Uri.EscapeDataString))}";

    public Task<List<StoreBanner>> GetAllAsync() =>
        _db.StoreBanners.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

    public Task<StoreBanner?> GetAsync(int id) =>
        _db.StoreBanners.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServiceResult> SaveAsync(BannerEditViewModel model)
    {
        var title = model.Title.Trim();
        if (string.IsNullOrWhiteSpace(title)) return ServiceResult.Fail("Tiêu đề banner là bắt buộc.");

        var url = NormalizeUrl(model.Url);
        if (model.Url is not null && !string.IsNullOrWhiteSpace(model.Url) && url is null)
            return ServiceResult.Fail("URL banner phải là đường dẫn http/https hoặc đường dẫn nội bộ bắt đầu bằng '/'.");

        var banner = model.Id > 0
            ? await _db.StoreBanners.FirstOrDefaultAsync(x => x.Id == model.Id)
            : new StoreBanner();
        if (model.Id > 0 && banner is null) return ServiceResult.Fail("Không tìm thấy banner cần cập nhật.");

        string? savedPath = null;
        string? oldPath = null;
        try
        {
            if (model.Image is not null)
            {
                var detected = await DetectImageTypeAsync(model.Image);
                if (detected is null)
                    return ServiceResult.Fail("Ảnh banner chỉ hỗ trợ JPG, PNG hoặc WebP.");
                if (model.Image.Length <= 0 || model.Image.Length > Math.Max(1, _options.MaxFileSizeMb) * 1024L * 1024L)
                    return ServiceResult.Fail($"Ảnh banner không được vượt quá {_options.MaxFileSizeMb} MB.");

                savedPath = await SaveFileAsync(model.Image);
                oldPath = banner!.ImagePath;
            }
            else if (banner is null || string.IsNullOrWhiteSpace(banner.ImagePath))
            {
                return ServiceResult.Fail("Vui lòng chọn ảnh banner.");
            }

            banner ??= new StoreBanner();
            banner.Title = title;
            banner.Url = url;
            banner.SortOrder = Math.Max(0, model.SortOrder);
            banner.IsActive = model.IsActive;
            if (savedPath is not null) banner.ImagePath = savedPath;

            if (banner.Id == 0) _db.StoreBanners.Add(banner);
            await _db.SaveChangesAsync();

            if (savedPath is not null && !string.IsNullOrWhiteSpace(oldPath))
                DeletePhysicalFile(oldPath);

            return ServiceResult.Ok("Đã lưu banner.", banner.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException or InvalidDataException)
        {
            _logger.LogError(ex, "Không thể lưu banner {BannerId}", model.Id);
            if (savedPath is not null) DeletePhysicalFile(savedPath);
            return ServiceResult.Fail("Không thể lưu banner. Vui lòng kiểm tra quyền ghi thư mục ảnh và thử lại.");
        }
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var banner = await _db.StoreBanners.FirstOrDefaultAsync(x => x.Id == id);
        if (banner is null) return ServiceResult.Fail("Không tìm thấy banner cần xóa.");

        _db.StoreBanners.Remove(banner);
        try
        {
            await _db.SaveChangesAsync();
            DeletePhysicalFile(banner.ImagePath);
            return ServiceResult.Ok("Đã xóa banner.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Không thể xóa banner {BannerId}", id);
            return ServiceResult.Fail("Không thể xóa banner.");
        }
    }

    private async Task<string> SaveFileAsync(IFormFile file)
    {
        var folder = Path.Combine(_rootPath, "banners");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var fullPath = Path.Combine(folder, fileName);

        using var source = file.OpenReadStream();
        Image image;
        try
        {
            image = await Image.LoadAsync(source);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidDataException("Ảnh banner không hợp lệ.", ex);
        }

        if ((long)image.Width * image.Height > MaxBannerPixels)
        {
            image.Dispose();
            throw new InvalidDataException("Ảnh banner có kích thước quá lớn.");
        }

        using (image)
        {
            image.Mutate(context => context
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(BannerWidth, BannerHeight),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

            await image.SaveAsJpegAsync(fullPath, new JpegEncoder { Quality = 88 });
        }
        return $"banners/{fileName}";
    }

    private void DeletePhysicalFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        try
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
            var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex, "Không thể xóa file banner {RelativePath}", relativePath);
        }
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        if (url.StartsWith('/') && !url.StartsWith("//")) return url;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;
    }

    private static async Task<DetectedImageType?> DetectImageTypeAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return new(".jpg");
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return new(".png");
        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return new(".webp");
        return null;
    }

    private sealed record DetectedImageType(string Extension);
}
