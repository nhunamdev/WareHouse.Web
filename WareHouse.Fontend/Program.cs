using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Localization;
using WareHouse.Data;
using WareHouse.Fontend.Models;
using WareHouse.Fontend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<StoreText>();
builder.Services.AddDbContext<WareHouseDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Warehouse")
        ?? throw new InvalidOperationException("ConnectionStrings:Warehouse is missing."),
        sql => sql.EnableRetryOnFailure(3)));
builder.Services.Configure<ProductImageOptions>(builder.Configuration.GetSection("ProductImages"));
builder.Services.Configure<StoreContactOptions>(builder.Configuration.GetSection(StoreContactOptions.SectionName));
builder.Services.AddScoped<CatalogService>();
builder.Services.AddHttpClient("AdminMedia", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        CultureInfo.GetCultureInfo("vi-VN"),
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("de-DE")
    };
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new CustomRequestCultureProvider(context =>
        {
            var code = context.Request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            var result = StoreCulture.IsSupported(code) && !string.Equals(code, "vi", StringComparison.OrdinalIgnoreCase)
                ? new ProviderCultureResult(StoreCulture.CultureName(code))
                : null;
            return Task.FromResult<ProviderCultureResult?>(result);
        }),
        new CookieRequestCultureProvider()
    ];
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRequestLocalization();
app.UseRouting();

var imageOptions = app.Configuration.GetSection("ProductImages").Get<ProductImageOptions>() ?? new();
var configuredImageRoots = GetAdminImageRoots(imageOptions, app.Environment.ContentRootPath);
var existingImageRoots = configuredImageRoots.Where(Directory.Exists).ToList();
foreach (var imageRoot in existingImageRoots)
{
    app.Logger.LogInformation("Serving product images from {ImageRoot} at {RequestPath}.", imageRoot, imageOptions.RequestPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imageRoot),
        RequestPath = imageOptions.RequestPath,
        OnPrepareResponse = context =>
            context.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable"
    });
}
if (existingImageRoots.Count == 0)
{
    app.Logger.LogWarning(
        "None of the configured admin image folders exist: {ImageRoots}. The admin media endpoint will be used as fallback.",
        string.Join("; ", configuredImageRoots));
}

var themeRoot = Path.Combine(app.Environment.ContentRootPath, "themes");
if (Directory.Exists(themeRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(themeRoot),
        RequestPath = "/themes"
    });
}

app.UseStaticFiles();
app.UseAuthorization();

var mediaRequestPath = "/" + imageOptions.RequestPath.Trim('/');
app.MapGet($"{mediaRequestPath}/{{**path}}", async (
    string? path,
    HttpContext context,
    IHttpClientFactory clientFactory,
    CancellationToken cancellationToken) =>
{
    var normalizedPath = (path ?? string.Empty).Replace('\\', '/').Trim('/');
    var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var extension = Path.GetExtension(normalizedPath);
    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    if (pathSegments.Length == 0 ||
        pathSegments.Any(segment => segment is "." or "..") ||
        !allowedExtensions.Contains(extension))
    {
        return Results.NotFound();
    }

    var currentImageRoots = GetAdminImageRoots(imageOptions, app.Environment.ContentRootPath);
    var physicalFile = FindImageFile(currentImageRoots, pathSegments);
    if (physicalFile is not null)
    {
        context.Response.Headers.CacheControl = "public,max-age=2592000,immutable";
        var physicalContentType = extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return Results.File(physicalFile, physicalContentType, enableRangeProcessing: true);
    }

    if (!Uri.TryCreate(imageOptions.SourceBaseUrl, UriKind.Absolute, out var sourceBaseUri) ||
        (sourceBaseUri.Scheme != Uri.UriSchemeHttp && sourceBaseUri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.NotFound();
    }

    if (string.Equals(sourceBaseUri.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase) &&
        (!sourceBaseUri.IsDefaultPort && sourceBaseUri.Port == context.Request.Host.Port ||
         sourceBaseUri.IsDefaultPort && context.Request.Host.Port is null))
    {
        return Results.NotFound();
    }

    var escapedPath = string.Join('/', pathSegments.Select(Uri.EscapeDataString));
    var sourceUrl = new Uri($"{sourceBaseUri.AbsoluteUri.TrimEnd('/')}/{escapedPath}");

    try
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
        using var response = await clientFactory.CreateClient("AdminMedia")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.NotFound();
        if (!response.IsSuccessStatusCode)
        {
            app.Logger.LogWarning(
                "Image source {SourceUrl} returned status {StatusCode}.",
                sourceUrl,
                (int)response.StatusCode);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        const long maxImageSize = 10 * 1024 * 1024;
        if (string.IsNullOrWhiteSpace(contentType) ||
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            response.Content.Headers.ContentLength > maxImageSize)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > maxImageSize)
            return Results.StatusCode(StatusCodes.Status502BadGateway);

        context.Response.Headers.CacheControl = "public,max-age=86400";
        return Results.File(bytes, contentType);
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        app.Logger.LogWarning(exception, "Cannot load image {ImagePath} from the admin website.", normalizedPath);
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
}).AllowAnonymous();

app.MapControllerRoute(
    name: "language",
    pattern: "language/{culture}",
    defaults: new { controller = "Home", action = "SetLanguage" });
app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "Seo", action = "Sitemap" });
app.MapControllerRoute(
    name: "robots",
    pattern: "robots.txt",
    defaults: new { controller = "Seo", action = "Robots" });
app.MapControllerRoute(
    name: "localized-product-search-suggestions",
    pattern: "{culture:regex(^(en|de)$)}/api/tim-kiem-goi-y",
    defaults: new { controller = "Home", action = "SearchSuggestions" });
app.MapControllerRoute(
    name: "product-search-suggestions",
    pattern: "api/tim-kiem-goi-y",
    defaults: new { controller = "Home", action = "SearchSuggestions" });
app.MapControllerRoute(
    name: "localized-product-search",
    pattern: "{culture:regex(^(en|de)$)}/tim-kiem",
    defaults: new { controller = "Home", action = "Search" });
app.MapControllerRoute(
    name: "product-search",
    pattern: "tim-kiem",
    defaults: new { controller = "Home", action = "Search" });
app.MapControllerRoute(
    name: "localized-category-bicycle",
    pattern: "{culture:regex(^(en|de)$)}/xe-dap",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dap" });
app.MapControllerRoute(
    name: "category-bicycle",
    pattern: "xe-dap",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dap" });
app.MapControllerRoute(
    name: "localized-category-electric",
    pattern: "{culture:regex(^(en|de)$)}/xe-dien",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dien" });
app.MapControllerRoute(
    name: "category-electric",
    pattern: "xe-dien",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dien" });
app.MapControllerRoute(
    name: "localized-about",
    pattern: "{culture:regex(^(en|de)$)}/ve-chung-toi",
    defaults: new { controller = "Home", action = "About" });
app.MapControllerRoute(
    name: "about",
    pattern: "ve-chung-toi",
    defaults: new { controller = "Home", action = "About" });
app.MapControllerRoute(
    name: "localized-product-details",
    pattern: "{culture:regex(^(en|de)$)}/xe/{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "product-details",
    pattern: "xe/{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "localized-product-slug",
    pattern: "{culture:regex(^(en|de)$)}/{slug}-{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "product-slug",
    pattern: "{slug}-{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "localized-home",
    pattern: "{culture:regex(^(en|de)$)}",
    defaults: new { controller = "Home", action = "Index" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();

static List<string> GetAdminImageRoots(ProductImageOptions options, string contentRootPath)
{
    var imageRoots = new[] { options.RootPath }
        .Concat(options.FallbackRootPaths ?? [])
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path)))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (options.DiscoverAdminSibling)
    {
        imageRoots.AddRange(DiscoverAdminImageRoots(contentRootPath)
            .Where(path => !imageRoots.Contains(path, StringComparer.OrdinalIgnoreCase)));
    }

    return imageRoots;
}

static string? FindImageFile(IEnumerable<string> imageRoots, IReadOnlyCollection<string> pathSegments)
{
    foreach (var imageRoot in imageRoots.Where(Directory.Exists))
    {
        var root = Path.GetFullPath(imageRoot);
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine([root, .. pathSegments]));
        if (candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
            return candidate;
    }

    return null;
}

static IEnumerable<string> DiscoverAdminImageRoots(string contentRootPath)
{
    var contentRoot = new DirectoryInfo(Path.GetFullPath(contentRootPath));
    var searchRoots = new[] { contentRoot.Parent, contentRoot.Parent?.Parent }
        .Where(directory => directory is not null)
        .Cast<DirectoryInfo>()
        .DistinctBy(directory => directory.FullName, StringComparer.OrdinalIgnoreCase);

    foreach (var searchRoot in searchRoots)
    {
        IEnumerable<string> siblingDirectories;
        try
        {
            siblingDirectories = Directory.EnumerateDirectories(searchRoot.FullName).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            continue;
        }

        foreach (var siblingDirectory in siblingDirectories)
        {
            var isAdminSite = File.Exists(Path.Combine(siblingDirectory, "WareHouse.Web.dll")) ||
                              File.Exists(Path.Combine(siblingDirectory, "WareHouse.Web.exe")) ||
                              File.Exists(Path.Combine(siblingDirectory, "WareHouse.Web.csproj"));
            if (!isAdminSite) continue;

            var imageRoot = Path.GetFullPath(Path.Combine(siblingDirectory, "data", "images"));
            if (Directory.Exists(imageRoot)) yield return imageRoot;
        }
    }
}
