using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class InventoryController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(
        int? productId, int? warehouseId, string? keyword, string? sort, string? direction)
    {
        // WareHouseServices shares one scoped DbContext for the request, so its EF queries
        // must complete sequentially instead of running concurrently.
        var products = await _services.GetActiveProductsAsync();
        var warehouses = await _services.GetWarehousesAsync(true);
        warehouseId = WareHouseServices.ResolveSingleWarehouseId(warehouses, warehouseId);
        var matrix = await _services.GetStockMatrixAsync(productId, keyword, warehouseId, sort, direction);
        var productGroups = matrix.Rows
            .GroupBy(x => x.ProductId)
            .Select(group =>
            {
                var first = group.First();
                return new InventoryProductGroupViewModel
                {
                    ProductId = group.Key,
                    ProductName = first.ProductName,
                    Category = first.ProductCategory,
                    Unit = first.ProductUnit,
                    CreatedAt = first.ProductCreatedAt,
                    Variants = group
                        .OrderByDescending(x => x.ItemId)
                        .ThenByDescending(x => x.TotalQuantity > 0)
                        .ToList()
                };
            }).ToList();

        ViewBag.Sort = sort;
        ViewBag.Direction = direction;

        return View(new InventoryViewModel
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Keyword = keyword,
            Products = products,
            Warehouses = warehouses,
            Matrix = matrix,
            ProductGroups = productGroups,
            Summary = new InventorySummaryViewModel
            {
                TotalQuantity = productGroups.Sum(x => x.TotalQuantity),
                InventoryValue = productGroups.Sum(x => x.InventoryValue),
                ProductCount = productGroups.Count,
                InStockProductCount = productGroups.Count(x => x.TotalQuantity > 0),
                OutOfStockProductCount = productGroups.Count(x => x.TotalQuantity < 1),
                NegativeStockProductCount = productGroups.Count(x => x.HasNegativeStock),
                MissingQuantity = productGroups.Sum(x => x.MissingQuantity)
            }
        });
    }
}
