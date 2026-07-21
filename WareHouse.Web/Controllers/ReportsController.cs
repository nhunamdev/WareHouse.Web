using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class ReportsController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Inventory(int? productId, int? warehouseId, string? keyword)
    {
        var warehouses = await _services.GetWarehousesAsync(true);
        warehouseId = WareHouseServices.ResolveSingleWarehouseId(warehouses, warehouseId);
        ViewBag.ProductId = productId;
        ViewBag.WarehouseId = warehouseId;
        ViewBag.Keyword = keyword;
        ViewBag.Products = await _services.GetActiveProductsAsync();
        ViewBag.Warehouses = warehouses;
        return View(await _services.GetInventoryReportAsync(productId, keyword, warehouseId));
    }

    public async Task<IActionResult> StockMovement(DateTime? fromDate, DateTime? toDate, int? warehouseId)
    {
        var from = fromDate?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = toDate?.Date ?? DateTime.Today;
        var warehouses = await _services.GetWarehousesAsync(true);
        warehouseId = WareHouseServices.ResolveSingleWarehouseId(warehouses, warehouseId);
        return View(new StockMovementReportViewModel
        {
            FromDate = from,
            ToDate = to,
            WarehouseId = warehouseId,
            Warehouses = warehouses,
            Rows = await _services.GetStockMovementReportAsync(from, to, warehouseId)
        });
    }

    public async Task<IActionResult> Sales(
        DateTime? fromDate, DateTime? toDate, string? keyword, int page = 1)
    {
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Keyword = keyword;
        var sales = await _services.GetSalesReportAsync(fromDate, toDate, keyword, page);
        var summary = await _services.GetSalesReportSummaryAsync(fromDate, toDate, keyword);
        return View(new SalesReportViewModel
        {
            Sales = sales,
            OrderCount = summary.OrderCount,
            Quantity = summary.Quantity,
            TotalAmount = summary.TotalAmount,
            PaidAmount = summary.PaidAmount,
            DebtAmount = summary.DebtAmount
        });
    }

    public async Task<IActionResult> Debt(DateTime? fromDate, DateTime? toDate) =>
        View(new DebtReportViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            Rows = await _services.GetDebtReportAsync(fromDate, toDate)
        });
}
