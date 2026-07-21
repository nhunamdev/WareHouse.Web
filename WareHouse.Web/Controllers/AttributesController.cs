using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class AttributesController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(string? keyword, string? sort, string? direction, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        return View(await _services.GetAttributesAsync(keyword, page, sort: sort, direction: direction));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new ProductAttribute { IsActive = true });
        var model = await _services.GetAttributeAsync(id.Value);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductAttribute model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _services.SaveAttributeAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Values(int id, string? sort, string? direction)
    {
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        var attribute = await _services.GetAttributeWithSortedValuesAsync(id, sort, direction);
        return attribute is null ? NotFound() : View(attribute);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveValue(AttributeValue model)
    {
        var result = await _services.SaveAttributeValueAsync(model);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Values), new { id = model.AttributeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteValue(int id, int attributeId)
    {
        var result = await _services.DeleteAttributeValueAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Values), new { id = attributeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _services.DeleteAttributeAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
