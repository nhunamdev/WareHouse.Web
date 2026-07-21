using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WareHouse.Data;
using WareHouse.Fontend.Models;
using WareHouse.Fontend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<WareHouseDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Warehouse")
        ?? throw new InvalidOperationException("ConnectionStrings:Warehouse is missing."),
        sql => sql.EnableRetryOnFailure(3)));
builder.Services.Configure<ProductImageOptions>(builder.Configuration.GetSection("ProductImages"));
builder.Services.Configure<StoreContactOptions>(builder.Configuration.GetSection(StoreContactOptions.SectionName));
builder.Services.AddScoped<CatalogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

var imageOptions = app.Configuration.GetSection("ProductImages").Get<ProductImageOptions>() ?? new();
var imageRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, imageOptions.RootPath));
if (Directory.Exists(imageRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imageRoot),
        RequestPath = imageOptions.RequestPath
    });
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

app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "Seo", action = "Sitemap" });
app.MapControllerRoute(
    name: "robots",
    pattern: "robots.txt",
    defaults: new { controller = "Seo", action = "Robots" });
app.MapControllerRoute(
    name: "category-electric",
    pattern: "xe-dien",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dien" });
app.MapControllerRoute(
    name: "category-bicycle",
    pattern: "xe-dap",
    defaults: new { controller = "Home", action = "Category", slug = "xe-dap" });
app.MapControllerRoute(
    name: "category-toy",
    pattern: "xe-do-choi",
    defaults: new { controller = "Home", action = "Category", slug = "xe-do-choi" });
app.MapControllerRoute(
    name: "category-other",
    pattern: "khac",
    defaults: new { controller = "Home", action = "Category", slug = "khac" });
app.MapControllerRoute(
    name: "product-details",
    pattern: "xe/{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "product-slug",
    pattern: "{slug}-{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
