using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public sealed class AuditLogsController(WareHouseServices services) : Controller
{
    private readonly WareHouseServices _services = services;

    public async Task<IActionResult> Index(
        string? userName,
        string? action,
        string? entityName,
        DateTime? fromDate,
        DateTime? toDate,
        string? keyword,
        string? sort,
        string? direction,
        int page = 1)
    {
        ViewBag.UserName = userName;
        ViewBag.Action = action;
        ViewBag.EntityName = entityName;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        ViewBag.Users = await _services.GetAuditLogUsersAsync();
        ViewBag.EntityNames = await _services.GetAuditLogEntityNamesAsync();

        return View(await _services.GetAuditLogsAsync(
            userName, action, entityName, fromDate, toDate, keyword, page,
            sort: sort, direction: direction));
    }
}
