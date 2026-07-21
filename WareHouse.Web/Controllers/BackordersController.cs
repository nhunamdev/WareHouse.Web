using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class BackordersController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(int? warehouseId, string? keyword)
    {
        var warehouses = await _services.GetWarehousesAsync(true);
        warehouseId = WareHouseServices.ResolveSingleWarehouseId(warehouses, warehouseId);
        return View(new BackorderViewModel
        {
            WarehouseId = warehouseId,
            Keyword = keyword,
            Warehouses = warehouses,
            Rows = await _services.GetNegativeStocksAsync(warehouseId, keyword)
        });
    }
}
