using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.SalesAccess)]
public class CustomersController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(string? keyword, string? sort, string? direction, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        return View(await _services.GetCustomersAsync(keyword, page, sort: sort, direction: direction));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new Customer { IsActive = true, CustomerType = CustomerType.Retail });
        var model = await _services.GetCustomerAsync(id.Value);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Customer model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _services.SaveCustomerAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, DateTime? fromDate, DateTime? toDate)
    {
        var customer = await _services.GetCustomerAsync(id);
        if (customer is null) return NotFound();
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        return View(new CustomerDetailsViewModel
        {
            Customer = customer,
            Statement = await _services.GetCustomerStatementAsync(id, fromDate, toDate),
            OutstandingSales = await _services.GetOutstandingSalesAsync(id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _services.DeleteCustomerAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
