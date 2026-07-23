using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Fontend.Models;
using WareHouse.Fontend.Services;

namespace WareHouse.Fontend.Controllers;

public sealed class HomeController(CatalogService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(q))
            return Redirect($"{StoreCulture.Path("/tim-kiem")}?q={Uri.EscapeDataString(q.Trim())}");

        return View(await catalog.GetHomeAsync(null, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q, CancellationToken cancellationToken)
    {
        return View(await catalog.SearchAsync(q, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SearchSuggestions(string? q, CancellationToken cancellationToken)
    {
        return Json(await catalog.GetSearchSuggestionsAsync(q, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Category(string slug, string? q, CancellationToken cancellationToken)
    {
        var category = StoreCategories.FindBySlug(slug);
        if (category is null) return NotFound();
        return View(await catalog.GetCategoryAsync(category, q, cancellationToken));
    }

    [HttpGet]
    public IActionResult About() => View();

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? slug, CancellationToken cancellationToken)
    {
        var model = await catalog.GetDetailsAsync(id, cancellationToken);
        if (model is null) return NotFound();
        if (!string.Equals(slug, model.Slug, StringComparison.OrdinalIgnoreCase))
            return RedirectPermanent(model.ProductUrl);
        return View(model);
    }

    [HttpGet]
    public IActionResult Privacy() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult SetLanguage(string culture, string? returnUrl)
    {
        var code = StoreCulture.IsSupported(culture) ? culture.ToLowerInvariant() : "vi";
        var cultureName = StoreCulture.CultureName(code);
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultureName)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        return LocalRedirect(StoreCulture.ForCulture(safeReturnUrl, code));
    }
}
