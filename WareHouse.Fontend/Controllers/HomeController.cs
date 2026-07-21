using Microsoft.AspNetCore.Mvc;
using WareHouse.Fontend.Models;
using WareHouse.Fontend.Services;

namespace WareHouse.Fontend.Controllers;

public sealed class HomeController(CatalogService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        return View(await catalog.GetHomeAsync(q, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Category(string slug, string? q, CancellationToken cancellationToken)
    {
        var category = StoreCategories.FindBySlug(slug);
        if (category is null) return NotFound();
        return View(await catalog.GetCategoryAsync(category, q, cancellationToken));
    }

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
}
