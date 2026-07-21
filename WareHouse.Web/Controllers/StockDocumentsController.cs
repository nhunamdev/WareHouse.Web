using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class StockDocumentsController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(
        StockDocumentType type = StockDocumentType.Import, DocumentStatus? status = null,
        DateTime? fromDate = null, DateTime? toDate = null, string? keyword = null,
        string? sort = null, string? direction = null, int page = 1)
    {
        if (type == StockDocumentType.Sale) return RedirectToAction("Index", "Sales");
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        if (type == StockDocumentType.Import)
            return View(await _services.GetImportHistoryAsync(fromDate, toDate, keyword, page, sort: sort, direction: direction));
        if (type == StockDocumentType.Export)
            return View(await _services.GetExportHistoryAsync(fromDate, toDate, keyword, page, sort: sort, direction: direction));
        if (type == StockDocumentType.Transfer)
            return View(await _services.GetTransferHistoryAsync(fromDate, toDate, keyword, page, sort: sort, direction: direction));
        return View(await _services.GetDocumentsAsync(type, status, fromDate, toDate, keyword, page, sort: sort, direction: direction));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        long? id, StockDocumentType type = StockDocumentType.Import,
        int? warehouseId = null, int? itemId = null,
        decimal? quantity = null, decimal? costPrice = null)
    {
        if (type == StockDocumentType.Sale) return RedirectToAction("Edit", "Sales", new { id });
        var model = new DocumentEditViewModel { DocumentType = type };
        if (id.HasValue)
        {
            var document = await _services.GetDocumentAsync(id.Value);
            if (document is null) return NotFound();
            if (document.Status != DocumentStatus.Draft) return RedirectToAction(nameof(Details), new { id });
            model = MapDocument(document);
        }
        else if (type == StockDocumentType.Import)
        {
            model.ToWarehouseId = warehouseId;
            if (itemId.HasValue)
            {
                model.Details =
                [
                    new DocumentLineViewModel
                    {
                        ItemId = itemId.Value,
                        Quantity = Math.Max(1, decimal.Truncate(quantity ?? 1)),
                        Price = Math.Max(0, costPrice ?? 0)
                    }
                ];
            }
        }
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentEditViewModel model)
    {
        if (model.DocumentType == StockDocumentType.Sale)
            return RedirectToAction("Edit", "Sales", new { id = model.Id });
        ModelState.Remove(nameof(model.DocumentDate));
        if (ModelState.IsValid)
        {
            var result = await _services.SaveAndCompleteDocumentAsync(model.ToInput());
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
        if (document is null) return NotFound();
        if (document.DocumentType == StockDocumentType.Sale)
            return RedirectToAction("Details", "Sales", new { id });
        return View(document);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Giữ action này để xử lý các chứng từ nháp đã tồn tại trước khi chuyển sang lưu một bước.
    public async Task<IActionResult> Complete(long id)
    {
        var result = await _services.CompleteDocumentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await _services.CancelDocumentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillOptionsAsync(DocumentEditViewModel model)
    {
        model.Warehouses = await _services.GetWarehousesAsync(true);
        if (model.DocumentType is StockDocumentType.Import or StockDocumentType.Return or StockDocumentType.Adjust)
            model.ToWarehouseId = WareHouseServices.ResolveSingleWarehouseId(model.Warehouses, model.ToWarehouseId);
        else if (model.DocumentType is StockDocumentType.Export or StockDocumentType.Sale)
            model.FromWarehouseId = WareHouseServices.ResolveSingleWarehouseId(model.Warehouses, model.FromWarehouseId);
        else if (model.DocumentType == StockDocumentType.Transfer)
            // A transfer needs two different warehouses; with one warehouse the destination stays unselected.
            model.FromWarehouseId = WareHouseServices.ResolveSingleWarehouseId(model.Warehouses, model.FromWarehouseId);
        model.Customers = await _services.GetActiveCustomersAsync();
        var requiresSourceStock = model.DocumentType is
            StockDocumentType.Export or StockDocumentType.Transfer or StockDocumentType.Sale;
        var itemWarehouseId = model.DocumentType == StockDocumentType.Import
            ? model.ToWarehouseId
            : model.FromWarehouseId;
        var requiresItemWarehouse = requiresSourceStock || model.DocumentType == StockDocumentType.Import;
        model.Items = requiresItemWarehouse && !itemWarehouseId.HasValue
            ? []
            : await _services.GetItemsForSelectionAsync(
                null, itemWarehouseId, null, requiresSourceStock,
                model.Details.Select(x => x.ItemId).ToList(),
                useCostPrice: model.DocumentType is StockDocumentType.Import
                    or StockDocumentType.Export or StockDocumentType.Transfer or StockDocumentType.Adjust);
        if (model.Details.Count == 0) model.Details.Add(new DocumentLineViewModel());
    }

    internal static DocumentEditViewModel MapDocument(StockDocument document) => new()
    {
        Id = document.Id,
        DocumentNo = document.DocumentNo,
        DocumentType = document.DocumentType,
        DocumentDate = document.DocumentDate,
        CustomerId = document.CustomerId,
        CustomerPhone = document.CustomerPhone ?? document.Customer?.Phone,
        FromWarehouseId = document.FromWarehouseId,
        ToWarehouseId = document.ToWarehouseId,
        PaidAmount = document.PaidAmount,
        Remark = document.Remark,
        Details = document.Details.Select(x => new DocumentLineViewModel
            { ItemId = x.ItemId, Quantity = x.Quantity, Price = x.Price }).ToList()
    };
}
