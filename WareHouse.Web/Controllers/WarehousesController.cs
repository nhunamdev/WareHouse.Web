using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class WarehousesController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(string? keyword, string? sort, string? direction, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        return View(await _services.GetWarehousesPagedAsync(keyword, page, sort: sort, direction: direction));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new Warehouse { IsActive = true });
        var model = await _services.GetWarehouseAsync(id.Value);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Warehouse model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _services.SaveWarehouseAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _services.DeleteWarehouseAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
