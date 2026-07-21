using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.SalesAccess)]
public class PaymentsController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(
        string? keyword, string? sort, string? direction, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        var summary = await _services.GetDebtCustomerSummaryAsync(keyword);
        ViewBag.TotalDebt = summary.TotalDebt;
        return View(await _services.GetDebtCustomersAsync(keyword, page, sort: sort, direction: direction));
    }

    public async Task<IActionResult> History(
        int? customerId, DateTime? fromDate, DateTime? toDate,
        string? sort, string? direction, int page = 1)
    {
        ViewBag.CustomerId = customerId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        ViewBag.Customers = await _services.GetActiveCustomersAsync();
        return View(await _services.GetPaymentsAsync(
            customerId, fromDate, toDate, page, sort: sort, direction: direction));
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var payment = await _services.GetPaymentAsync(id);
        return payment is null ? NotFound() : View(payment);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, long? documentId)
    {
        var model = new PaymentEditViewModel
        {
            CustomerId = customerId ?? 0,
            DocumentId = documentId
        };
        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentEditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _services.CreatePaymentAsync(new Payment
            {
                CustomerId = model.CustomerId,
                Amount = model.Amount,
                PaymentDate = model.PaymentDate,
                PaymentMethod = model.PaymentMethod,
                Remark = model.Remark,
                Allocations = MapAllocations(model)
            });
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

    [HttpGet]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Edit(long id)
    {
        var payment = await _services.GetPaymentAsync(id);
        if (payment is null) return NotFound();
        if (payment.Remark == "Thanh toán khi bán hàng")
        {
            TempData["Error"] = "Thanh toán tạo cùng đơn bán không thể sửa riêng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new PaymentEditViewModel
        {
            Id = payment.Id,
            OriginalCustomerId = payment.CustomerId,
            OriginalDocumentId = payment.DocumentId,
            OriginalStatus = payment.Status,
            CustomerId = payment.CustomerId,
            DocumentId = payment.DocumentId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            Remark = payment.Remark,
            Allocations = payment.Allocations.Select(x => new PaymentAllocationEditViewModel
            {
                DocumentId = x.DocumentId,
                Selected = true,
                Amount = x.Amount
            }).ToList()
        };
        if (model.Allocations.Count == 0 && payment.DocumentId.HasValue)
            model.Allocations.Add(new PaymentAllocationEditViewModel
            {
                DocumentId = payment.DocumentId.Value,
                Selected = true,
                Amount = payment.Amount
            });
        await FillOptionsAsync(model);
        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Issue(long id)
    {
        var result = await _services.IssuePaymentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success
            ? RedirectToAction(nameof(PrintReceipt), new { id, autoPrint = true })
            : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Complete(long id)
    {
        var result = await _services.CompletePaymentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Cancel(long id)
    {
        var result = await _services.CancelPaymentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> PrintReceipt(long id, bool autoPrint = false)
    {
        var payment = await _services.GetPaymentAsync(id);
        if (payment is null) return NotFound();
        if (payment.Status is not (PaymentStatus.Issued or PaymentStatus.Completed))
            return RedirectToAction(nameof(Details), new { id });
        ViewBag.AutoPrint = autoPrint;
        return View(payment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Edit(PaymentEditViewModel model)
    {
        if (model.Id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await _services.UpdatePaymentAsync(new Payment
            {
                Id = model.Id,
                CustomerId = model.CustomerId,
                Amount = model.Amount,
                PaymentDate = model.PaymentDate,
                PaymentMethod = model.PaymentMethod,
                Remark = model.Remark,
                Allocations = MapAllocations(model)
            });
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }

        await FillOptionsAsync(model);
        return View("Create", model);
    }

    [HttpGet]
    public async Task<IActionResult> Outstanding(int customerId, string? includeDocumentIds = null) =>
        Json((await _services.GetOutstandingSalesAsync(customerId,
                (includeDocumentIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => long.TryParse(x, out var id) ? id : 0).Where(x => x > 0).ToList())).Select(x => new
            { x.Id, x.DocumentDate, x.TotalAmount, x.PaidAmount, x.DebtAmount }));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _services.DeletePaymentAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(History));
    }

    private async Task FillOptionsAsync(PaymentEditViewModel model)
    {
        model.Customers = (await _services.GetActiveCustomersAsync())
            .Where(x => x.Debt > 0 || x.Id == model.CustomerId)
            .ToList();
        var submittedAllocations = model.Allocations.GroupBy(x => x.DocumentId)
            .ToDictionary(x => x.Key, x => x.Last());
        var includedDocumentIds = model.OriginalStatus == PaymentStatus.Completed &&
                                  model.CustomerId == model.OriginalCustomerId
            ? submittedAllocations.Values.Where(x => x.Selected).Select(x => x.DocumentId)
                .Concat(model.OriginalDocumentId.HasValue ? [model.OriginalDocumentId.Value] : [])
                .Distinct().ToList()
            : [];
        model.OutstandingSales = model.CustomerId > 0
            ? await _services.GetOutstandingSalesAsync(model.CustomerId, includedDocumentIds)
            : [];
        model.Allocations = model.OutstandingSales.Select(sale =>
        {
            submittedAllocations.TryGetValue(sale.Id, out var submitted);
            var selectedFromRoute = model.Id == 0 && model.DocumentId == sale.Id;
            var appliedAmount = model.OriginalStatus == PaymentStatus.Completed &&
                                model.CustomerId == model.OriginalCustomerId && submitted?.Selected == true
                ? submitted.Amount
                : 0;
            return new PaymentAllocationEditViewModel
            {
                DocumentId = sale.Id,
                Selected = submitted?.Selected == true || selectedFromRoute,
                Amount = submitted?.Amount > 0
                    ? submitted.Amount
                    : selectedFromRoute ? sale.DebtAmount : 0,
                DocumentDate = sale.DocumentDate,
                TotalAmount = sale.TotalAmount,
                PaidAmount = sale.PaidAmount,
                AvailableDebt = sale.DebtAmount + appliedAmount
            };
        }).ToList();
        var selectedTotal = model.Allocations.Where(x => x.Selected).Sum(x => x.Amount);
        if (selectedTotal > 0) model.Amount = selectedTotal;
    }

    private static List<PaymentAllocation> MapAllocations(PaymentEditViewModel model) =>
        model.Allocations.Where(x => x.Selected && x.DocumentId > 0 && x.Amount > 0)
            .Select(x => new PaymentAllocation { DocumentId = x.DocumentId, Amount = x.Amount })
            .ToList();
}
