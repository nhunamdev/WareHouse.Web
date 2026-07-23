using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ModelBinding;
using WareHouse.Web.Models;
using WareHouse.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình connection string 'DefaultConnection'.");

builder.Services.AddHttpContextAccessor();
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("WareHouse.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddScoped<IAuditUserProvider, HttpAuditUserProvider>();
builder.Services.AddDbContext<WareHouseDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        // Keep the default seed credentials easy to enter while retaining
        // the required length, digit and lowercase checks.
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "WareHouse.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.MaxAge = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(30));
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.Configure<ProductCatalogSettings>(
    builder.Configuration.GetSection(ProductCatalogSettings.SectionName));
builder.Services.Configure<ProductImageStorageOptions>(
    builder.Configuration.GetSection(ProductImageStorageOptions.SectionName));
builder.Services.AddScoped<WareHouseServices>();
builder.Services.AddScoped<ProductImageService>();
builder.Services.AddScoped<StoreBannerService>();
builder.Services.AddControllersWithViews(options =>
    options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider()));
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var culture = CultureInfo.GetCultureInfo("vi-VN");
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
    options.RequestCultureProviders.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
var productImageOptions = builder.Configuration
    .GetSection(ProductImageStorageOptions.SectionName)
    .Get<ProductImageStorageOptions>() ?? new ProductImageStorageOptions();
var productImageRoot = ProductImageService.ResolveRootPath(
    productImageOptions.RootPath, app.Environment.ContentRootPath);
Directory.CreateDirectory(productImageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productImageRoot),
    RequestPath = productImageOptions.RequestPath,
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable"
});
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

var publicMediaRequestPath = "/" + productImageOptions.RequestPath.Trim('/');
app.MapGet($"{publicMediaRequestPath}/{{**path}}", (string? path) =>
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

    var relativePath = Path.Combine(pathSegments);
    var fullPath = Path.GetFullPath(Path.Combine(productImageRoot, relativePath));
    var rootWithSeparator = productImageRoot.TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        return Results.NotFound();

    var contentType = extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
    return Results.File(fullPath, contentType, enableRangeProcessing: true);
}).AllowAnonymous();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await using (var migrationScope = app.Services.CreateAsyncScope())
{
    var warehouseDb = migrationScope.ServiceProvider.GetRequiredService<WareHouseDbContext>();
    await warehouseDb.Database.MigrateAsync();
}
await IdentityDataSeeder.SeedAsync(app.Services, app.Configuration);
app.Run();
