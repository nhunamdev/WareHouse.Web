using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.SalesAccess)]
public class ItemsController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    [Authorize(Roles = AppRoles.AdminOrManager)]
    public IActionResult Index(int? productId, string? keyword, int page = 1)
    {
        return productId.HasValue
            ? RedirectToAction("Edit", "Products", new { id = productId.Value })
            : RedirectToAction("Index", "Products", new { keyword, page });
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return RedirectToAction("Index", "Products");

        var item = await _services.GetItemAsync(id.Value);
        return item is null
            ? NotFound()
            : RedirectToAction("Edit", "Products", new { id = item.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Edit(ItemEditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = model.Id == 0
                ? await _services.CreateItemsAsync(
                    model.ProductId,
                    model.Rows.Select(x => new ProductItemInput
                    {
                        CostPrice = x.CostPrice,
                        SalePrice = x.SalePrice,
                        AttributeValueIds = x.AttributeValueIds
                    }).ToList())
                : await _services.SaveItemAsync(new Item
                {
                    Id = model.Id,
                    ProductId = model.ProductId,
                    Code = model.Code ?? string.Empty,
                    Barcode = model.Barcode,
                    CostPrice = model.CostPrice,
                    SalePrice = model.SalePrice,
                    IsActive = model.IsActive
                }, model.AttributeValueIds);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Index), new
                {
                    productId = model.Id == 0 ? model.ProductId : (int?)null
                });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _services.DeleteItemAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        int? productId, int? warehouseId, string? keyword,
        bool inStockOnly = false, bool useCostPrice = false) =>
        Json(await _services.GetItemsForSelectionAsync(
            productId, warehouseId, keyword, inStockOnly, useCostPrice: useCostPrice));

    private async Task FillOptionsAsync(ItemEditViewModel model)
    {
        model.Products = await _services.GetActiveProductsAsync();
        model.AttributeGroups = await _services.GetActiveAttributesAsync();
    }
}
