using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Web.Identity;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.SalesAccess)]
[Route("huong-dan")]
public class GuideController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
