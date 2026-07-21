using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace WareHouse.Data;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}

public abstract class AuditableEntity : IAuditableEntity
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

public interface IAuditUserProvider
{
    string CurrentUser { get; }
    string? IpAddress => null;
    string? HttpMethod => null;
    string? RequestPath => null;
}

public static class AuditTrail
{
    public const string SystemUser = "system";
    public const int UserNameMaxLength = 100;

    public static void Apply(ChangeTracker changeTracker, IAuditUserProvider? userProvider)
    {
        changeTracker.DetectChanges();

        var now = DateTime.Now;
        var currentUser = NormalizeUserName(userProvider?.CurrentUser);

        foreach (var entry in changeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = currentUser;
                entry.Entity.UpdatedAt = null;
                entry.Entity.UpdatedBy = null;
                continue;
            }

            if (entry.State != EntityState.Modified) continue;

            entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
            entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedBy = currentUser;
            entry.Property(nameof(IAuditableEntity.UpdatedAt)).IsModified = true;
            entry.Property(nameof(IAuditableEntity.UpdatedBy)).IsModified = true;
        }
    }

    internal static string NormalizeUserName(string? userName)
    {
        var value = string.IsNullOrWhiteSpace(userName) ? SystemUser : userName.Trim();
        return value.Length <= UserNameMaxLength ? value : value[..UserNameMaxLength];
    }
}
