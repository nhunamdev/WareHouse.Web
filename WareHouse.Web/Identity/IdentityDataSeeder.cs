using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WareHouse.Data;

namespace WareHouse.Web.Identity;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSucceeded(roleResult, $"Không thể tạo vai trò {role}.");
            }
        }

        var seedSection = configuration.GetSection("SeedAdmin");
        var userName = seedSection["UserName"]?.Trim();
        var password = seedSection["Password"];
        var fullName = seedSection["FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return;

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        if (user is not null)
        {
            if (!user.LockoutEnabled)
            {
                user.LockoutEnabled = true;
                EnsureSucceeded(await userManager.UpdateAsync(user),
                    "Không thể cập nhật bảo mật tài khoản Admin.");
            }
            return;
        }

        if (await userManager.Users.AnyAsync())
            return;

        user = new ApplicationUser
        {
            UserName = userName,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Quản trị hệ thống" : fullName,
            IsActive = true,
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTime.Now,
            CreatedBy = AuditTrail.SystemUser
        };
        var createResult = await userManager.CreateAsync(user, password);
        EnsureSucceeded(createResult, "Không thể tạo tài khoản Admin ban đầu.");
        var addRoleResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        EnsureSucceeded(addRoleResult, "Không thể gán quyền Admin ban đầu.");
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(
            $"{message} {string.Join(" ", result.Errors.Select(x => x.Description))}");
    }
}
