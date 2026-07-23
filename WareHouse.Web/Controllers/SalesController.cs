using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.SalesAccess)]
public class SalesController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(
        DateTime? fromDate, DateTime? toDate, string? keyword,
        string? sort, string? direction, int page = 1)
    {
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        var sales = await _services.GetSalesHistoryAsync(
            fromDate, toDate, keyword, page, sort: sort, direction: direction,
            includePending: true);
        var summary = await _services.GetSalesHistorySummaryAsync(fromDate, toDate, keyword);
        ViewBag.TotalSalesAmount = summary.TotalAmount;
        ViewBag.TotalDebtAmount = summary.DebtAmount;
        ViewBag.PendingCount = summary.PendingCount;
        return View(sales);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long? id)
    {
        if (id.HasValue && User.IsInRole(AppRoles.Sale) &&
            !User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Manager))
            return Forbid();
        var model = new DocumentEditViewModel { DocumentType = StockDocumentType.Sale };
        if (id.HasValue)
        {
            var document = await _services.GetDocumentAsync(id.Value);
            if (document is null || document.DocumentType != StockDocumentType.Sale) return NotFound();
            if (document.Status != DocumentStatus.Draft) return RedirectToAction(nameof(Details), new { id });
            model = StockDocumentsController.MapDocument(document);
        }
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentEditViewModel model)
    {
        if (model.Id > 0 && User.IsInRole(AppRoles.Sale) &&
            !User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Manager))
            return Forbid();
        model.DocumentType = StockDocumentType.Sale;
        ModelState.Remove(nameof(model.DocumentDate));
        if (ModelState.IsValid)
        {
            var result = await _services.CreateSaleAsync(model.ToInput());
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        await FillOptionsAsync(model);
        return View(model);
    }

    public async Task<IActionResult> Details(long id)
    {
        var document = await _services.GetDocumentAsync(id);
        return document is null || document.DocumentType != StockDocumentType.Sale ? NotFound() : View(document);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Complete(long id, decimal? paidAmount)
    {
        var result = await _services.CompleteDocumentAsync(id, paidAmount);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> IssueInvoice(long id)
    {
        var result = await _services.IssueSaleInvoiceAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success
            ? RedirectToAction(nameof(PrintInvoice), new { id, autoPrint = true })
            : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await _services.CancelDocumentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> PrintInvoice(long id, bool autoPrint = false)
    {
        var document = await _services.GetDocumentAsync(id);
        if (document is null || document.DocumentType != StockDocumentType.Sale) return NotFound();
        if (document.Status is not (DocumentStatus.Invoiced or DocumentStatus.Completed))
            return RedirectToAction(nameof(Details), new { id });
        ViewBag.AutoPrint = autoPrint;
        return View(document);
    }

    public async Task<IActionResult> PrintDelivery(long id)
    {
        var document = await _services.GetDocumentAsync(id);
        if (document is null || document.DocumentType != StockDocumentType.Sale) return NotFound();
        if (document.Status != DocumentStatus.Completed)
            return RedirectToAction(nameof(Details), new { id });
        return View(document);
    }

    private async Task FillOptionsAsync(DocumentEditViewModel model)
    {
        model.Warehouses = await _services.GetWarehousesAsync(true);
        model.FromWarehouseId = WareHouseServices.ResolveSingleWarehouseId(model.Warehouses, model.FromWarehouseId);
        model.Customers = await _services.GetActiveCustomersAsync();
        model.PreviousDebtAmount = model.CustomerId.HasValue
            ? await _services.GetCustomerOutstandingDebtAsync(model.CustomerId.Value)
            : 0;
        model.Items = !model.FromWarehouseId.HasValue
            ? []
            : await _services.GetItemsForSelectionAsync(
                null, model.FromWarehouseId, null, inStockOnly: false,
                model.Details.Select(x => x.ItemId).ToList());
        if (model.Details.Count == 0) model.Details.Add(new DocumentLineViewModel());
    }
}
