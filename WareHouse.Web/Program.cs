using System.Globalization;
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
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
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
    RequestPath = productImageOptions.RequestPath
});
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

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
