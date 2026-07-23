using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    WareHouseServices services,
    ILogger<AccountController> logger) : Controller
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly WareHouseServices _services = services;
    private readonly ILogger<AccountController> _logger = logger;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectAfterLogin(returnUrl, User.IsInRole(AppRoles.Sale));
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel { RememberMe = true });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByNameAsync(model.UserName.Trim());
        if (user is null || !user.IsActive)
        {
            await TryWriteAuditAsync(
                AuditLogActions.LoginFailed,
                model.UserName.Trim(),
                "Đăng nhập thất bại: tài khoản không tồn tại hoặc đã bị khóa.",
                userName: model.UserName.Trim());
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await TryWriteAuditAsync(
                AuditLogActions.Login,
                user.Id,
                "Đăng nhập hệ thống thành công.",
                userName: user.UserName);
            var saleOnly = await _userManager.IsInRoleAsync(user, AppRoles.Sale) &&
                           !await _userManager.IsInRoleAsync(user, AppRoles.Admin) &&
                           !await _userManager.IsInRoleAsync(user, AppRoles.Manager);
            return RedirectAfterLogin(returnUrl, saleOnly);
        }

        await TryWriteAuditAsync(
            AuditLogActions.LoginFailed,
            user.Id,
            result.IsLockedOut
                ? "Đăng nhập thất bại: tài khoản bị khóa tạm thời."
                : "Đăng nhập thất bại: mật khẩu không đúng.",
            new { result.IsLockedOut },
            user.UserName);

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Tài khoản tạm khóa 15 phút do đăng nhập sai quá nhiều lần."
            : "Tên đăng nhập hoặc mật khẩu không đúng.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var userName = User.Identity?.Name;
        await TryWriteAuditAsync(
            AuditLogActions.Logout,
            userId,
            "Đăng xuất khỏi hệ thống.",
            userName: userName);
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task TryWriteAuditAsync(
        string action,
        string? entityId,
        string description,
        object? changes = null,
        string? userName = null)
    {
        try
        {
            await _services.WriteAuditLogAsync(
                action, "Authentication", entityId, description, changes, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể ghi audit log cho thao tác {AuditAction}", action);
        }
    }

    private IActionResult RedirectAfterLogin(string? returnUrl, bool saleOnly)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return saleOnly
            ? RedirectToAction("Index", "Sales")
            : RedirectToAction("Index", "Home");
    }
}
