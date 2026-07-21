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
        var products = await db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => new { x.Id, x.Name, x.UpdatedAt, x.CreatedAt })
            .ToListAsync(cancellationToken);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlSet = new XElement(ns + "urlset",
            new XElement(ns + "url", new XElement(ns + "loc", baseUrl + "/"), new XElement(ns + "changefreq", "daily"), new XElement(ns + "priority", "1.0")),
            StoreCategories.All.Select(category => new XElement(ns + "url",
                new XElement(ns + "loc", baseUrl + category.Url),
                new XElement(ns + "changefreq", "daily"),
                new XElement(ns + "priority", "0.9"))),
            products.Select(product => new XElement(ns + "url",
                new XElement(ns + "loc", baseUrl + SlugHelper.ProductUrl(product.Id, product.Name)),
                new XElement(ns + "lastmod", (product.UpdatedAt ?? product.CreatedAt).ToUniversalTime().ToString("yyyy-MM-dd")),
                new XElement(ns + "changefreq", "weekly"),
                new XElement(ns + "priority", "0.8"))));

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
}
