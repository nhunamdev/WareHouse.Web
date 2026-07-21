using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.Models;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class HomeController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        return View(await _services.GetDashboardAsync(fromDate, toDate));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
