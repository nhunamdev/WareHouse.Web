using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class UserAdminController(
    UserManager<ApplicationUser> userManager,
    ApplicationIdentityDbContext identityDb,
    WareHouseServices services,
    ILogger<UserAdminController> logger) : Controller
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ApplicationIdentityDbContext _identityDb = identityDb;
    private readonly WareHouseServices _services = services;
    private readonly ILogger<UserAdminController> _logger = logger;

    public async Task<IActionResult> Index(string? sort, string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        var usersQuery = _identityDb.Users.AsNoTracking()
            .Select(user => new
            {
                User = user,
                Role = _identityDb.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(_identityDb.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (_, role) => role.Name)
                    .FirstOrDefault()
            });
        var ordered = (sort?.Trim().ToLowerInvariant(), descending) switch
        {
            ("username", true) => usersQuery.OrderByDescending(x => x.User.UserName),
            ("fullname", true) => usersQuery.OrderByDescending(x => x.User.FullName),
            ("fullname", false) => usersQuery.OrderBy(x => x.User.FullName),
            ("role", true) => usersQuery.OrderByDescending(x => x.Role),
            ("role", false) => usersQuery.OrderBy(x => x.Role),
            ("created", true) => usersQuery.OrderByDescending(x => x.User.CreatedAt),
            ("created", false) => usersQuery.OrderBy(x => x.User.CreatedAt),
            ("status", true) => usersQuery.OrderByDescending(x => x.User.IsActive),
            ("status", false) => usersQuery.OrderBy(x => x.User.IsActive),
            ("username", false) => usersQuery.OrderBy(x => x.User.UserName),
            _ => usersQuery.OrderByDescending(x => x.User.CreatedAt)
        };
        var model = await ordered.ThenByDescending(x => x.User.Id)
            .Select(x => new UserListItemViewModel
            {
                Id = x.User.Id,
                UserName = x.User.UserName ?? string.Empty,
                FullName = x.User.FullName,
                Role = x.Role ?? string.Empty,
                IsActive = x.User.IsActive,
                CreatedAt = x.User.CreatedAt,
                CreatedBy = x.User.CreatedBy,
                UpdatedAt = x.User.UpdatedAt,
                UpdatedBy = x.User.UpdatedBy
            })
            .ToListAsync();
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        ViewBag.Roles = AppRoles.All;
        if (string.IsNullOrWhiteSpace(id))
            return View(new UserEditViewModel { IsActive = true, Role = AppRoles.Sale });

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        return View(new UserEditViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? AppRoles.Sale,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            UpdatedAt = user.UpdatedAt,
            UpdatedBy = user.UpdatedBy
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        ViewBag.Roles = AppRoles.All;
        if (!AppRoles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
        if (string.IsNullOrWhiteSpace(model.Id) && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu cho tài khoản mới.");
        if (!ModelState.IsValid) return View(model);

        if (string.IsNullOrWhiteSpace(model.Id))
            return await CreateUserAsync(model);

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        if (user.Id == _userManager.GetUserId(User) &&
            (!model.IsActive || model.Role != AppRoles.Admin))
        {
            ModelState.AddModelError(string.Empty,
                "Bạn không thể tự vô hiệu hóa hoặc bỏ quyền Admin của chính mình.");
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains(AppRoles.Admin) &&
            (!model.IsActive || model.Role != AppRoles.Admin) &&
            await IsLastActiveAdminAsync(user.Id))
        {
            ModelState.AddModelError(string.Empty, "Hệ thống phải còn ít nhất một tài khoản Admin hoạt động.");
            return View(model);
        }

        user.UserName = model.UserName.Trim();
        user.FullName = model.FullName.Trim();
        user.IsActive = model.IsActive;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!AddIdentityErrors(updateResult)) return View(model);

        if (!currentRoles.Contains(model.Role))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!AddIdentityErrors(addRoleResult)) return View(model);
        }
        var rolesToRemove = currentRoles.Where(x => x != model.Role).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!AddIdentityErrors(removeResult)) return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
            if (!AddIdentityErrors(passwordResult)) return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await TryWriteAuditAsync(
            AuditLogActions.Update,
            user.Id,
            $"Cập nhật tài khoản {user.UserName}.",
            new
            {
                Role = model.Role,
                model.IsActive,
                PasswordChanged = !string.IsNullOrWhiteSpace(model.Password)
            });
        TempData["Success"] = "Đã cập nhật tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is null
            ? NotFound()
            : View(new ResetPasswordViewModel { Id = user.Id, UserName = user.UserName ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.Password);
        if (!AddIdentityErrors(result)) return View(model);
        await _userManager.UpdateSecurityStampAsync(user);
        await TryWriteAuditAsync(
            AuditLogActions.ResetPassword,
            user.Id,
            $"Đặt lại mật khẩu cho tài khoản {user.UserName}.");
        TempData["Success"] = $"Đã đặt lại mật khẩu cho {user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "Bạn không thể tự vô hiệu hóa tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (user.IsActive && roles.Contains(AppRoles.Admin) && await IsLastActiveAdminAsync(user.Id))
        {
            TempData["Error"] = "Hệ thống phải còn ít nhất một tài khoản Admin hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _userManager.UpdateSecurityStampAsync(user);
            await TryWriteAuditAsync(
                AuditLogActions.ChangeStatus,
                user.Id,
                user.IsActive
                    ? $"Mở lại tài khoản {user.UserName}."
                    : $"Vô hiệu hóa tài khoản {user.UserName}.",
                new { user.IsActive });
            TempData["Success"] = user.IsActive ? "Đã mở lại tài khoản." : "Đã vô hiệu hóa tài khoản.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description));
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "Bạn không thể xóa tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin) && await IsLastActiveAdminAsync(user.Id))
        {
            TempData["Error"] = "Hệ thống phải còn ít nhất một tài khoản Admin.";
            return RedirectToAction(nameof(Index));
        }
        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            await TryWriteAuditAsync(
                AuditLogActions.Delete,
                user.Id,
                $"Xóa tài khoản {user.UserName}.");
        }
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "Đã xóa tài khoản."
            : string.Join(" ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> CreateUserAsync(UserEditViewModel model)
    {
        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            FullName = model.FullName.Trim(),
            IsActive = model.IsActive,
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTime.Now,
            CreatedBy = User.Identity?.Name
        };
        var createResult = await _userManager.CreateAsync(user, model.Password!);
        if (!AddIdentityErrors(createResult)) return View("Edit", model);
        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View("Edit", model);
        }
        await TryWriteAuditAsync(
            AuditLogActions.Create,
            user.Id,
            $"Tạo tài khoản {user.UserName}.",
            new { model.Role, model.IsActive });
        TempData["Success"] = "Đã tạo tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    private bool AddIdentityErrors(IdentityResult result)
    {
        if (result.Succeeded) return true;
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return false;
    }

    private async Task<bool> IsLastActiveAdminAsync(string excludedUserId)
    {
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        return admins.Count(x => x.IsActive && x.Id != excludedUserId) == 0;
    }

    private async Task TryWriteAuditAsync(
        string action,
        string? userId,
        string description,
        object? changes = null)
    {
        try
        {
            await _services.WriteAuditLogAsync(
                action, nameof(ApplicationUser), userId, description, changes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể ghi audit log cho thao tác tài khoản {AuditAction}", action);
        }
    }
}
