using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WareHouse.Data;
using WareHouse.Fontend.Models;
using WareHouse.Fontend.Services;

namespace WareHouse.Fontend.Controllers;

public sealed class SeoController(WareHouseDbContext db, IConfiguration configuration) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var baseUrl = SiteUrl();
        var visibleCategories = StoreCategories.All.Select(x => x.Name).ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Category != null && visibleCategories.Contains(x.Category))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new { x.Id, x.Name, x.NameEn, x.NameDe, x.UpdatedAt, x.CreatedAt })
            .ToListAsync(cancellationToken);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var cultures = StoreCulture.SupportedCodes;
        var staticUrls = cultures.SelectMany(culture => new[]
        {
            SitemapUrl(ns, baseUrl + StoreCulture.ForCulture("/", culture), "daily", "1.0"),
            SitemapUrl(ns, baseUrl + StoreCulture.ForCulture("/ve-chung-toi", culture), "monthly", "0.6")
        });
        var categoryUrls = cultures.SelectMany(culture => StoreCategories.All.Select(category =>
            SitemapUrl(ns, baseUrl + StoreCulture.ForCulture($"/{category.Slug}", culture), "daily", "0.9")));
        var productUrls = cultures.SelectMany(culture => products.Select(product =>
        {
            var name = culture switch
            {
                "en" when !string.IsNullOrWhiteSpace(product.NameEn) => product.NameEn,
                "de" when !string.IsNullOrWhiteSpace(product.NameDe) => product.NameDe,
                _ => product.Name
            };
            return new XElement(ns + "url",
                new XElement(ns + "loc", baseUrl + StoreCulture.ForCulture(SlugHelper.ProductUrl(product.Id, name), culture)),
                new XElement(ns + "lastmod", (product.UpdatedAt ?? product.CreatedAt).ToUniversalTime().ToString("yyyy-MM-dd")),
                new XElement(ns + "changefreq", "weekly"),
                new XElement(ns + "priority", "0.8"));
        }));
        var urlSet = new XElement(ns + "urlset", staticUrls, categoryUrls, productUrls);

        return Content(new XDocument(new XDeclaration("1.0", "utf-8", "yes"), urlSet).ToString(), "application/xml; charset=utf-8");
    }

    [HttpGet]
    public IActionResult Robots() => Content($"User-agent: *{Environment.NewLine}Allow: /{Environment.NewLine}Sitemap: {SiteUrl()}/sitemap.xml{Environment.NewLine}", "text/plain; charset=utf-8");

    private string SiteUrl()
    {
        var configured = configuration["Seo:SiteUrl"]?.TrimEnd('/');
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : $"{Request.Scheme}://{Request.Host}";
    }

    private static XElement SitemapUrl(XNamespace ns, string location, string frequency, string priority) =>
        new(ns + "url",
            new XElement(ns + "loc", location),
            new XElement(ns + "changefreq", frequency),
            new XElement(ns + "priority", priority));
}
