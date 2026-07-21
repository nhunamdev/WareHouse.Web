using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace WareHouse.Data;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    [Required, StringLength(100)]
    public string UserName { get; set; } = AuditTrail.SystemUser;

    [Required, StringLength(30)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? EntityId { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public string? Changes { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(10)]
    public string? HttpMethod { get; set; }

    [StringLength(500)]
    public string? RequestPath { get; set; }
}

public static class AuditLogActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string ResetPassword = "ResetPassword";
    public const string ChangeStatus = "ChangeStatus";
}

public static class AuditLogTrail
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IAuditableEntity.CreatedAt),
        nameof(IAuditableEntity.CreatedBy),
        nameof(IAuditableEntity.UpdatedAt),
        nameof(IAuditableEntity.UpdatedBy),
        "RowVersion"
    };

    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp"
    };

    public static IReadOnlyList<AuditLog> CreateAutomaticLogs(
        ChangeTracker changeTracker,
        IAuditUserProvider? userProvider)
    {
        changeTracker.DetectChanges();
        var logs = new List<AuditLog>();

        foreach (var entry in changeTracker.Entries()
                     .Where(x => x.Entity is not AuditLog &&
                                 x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var changes = CollectChanges(entry);
            if (entry.State == EntityState.Modified && changes.Count == 0) continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditLogActions.Create,
                EntityState.Modified => AuditLogActions.Update,
                _ => AuditLogActions.Delete
            };
            var entityName = entry.Metadata.ClrType.Name;
            logs.Add(CreateManual(
                action,
                entityName,
                GetEntityId(entry),
                $"{ActionLabel(action)} {entityName}",
                changes.Count == 0 ? null : JsonSerializer.Serialize(changes),
                userProvider));
        }

        return logs;
    }

    public static AuditLog CreateManual(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? changes,
        IAuditUserProvider? userProvider,
        string? userName = null) => new()
        {
            OccurredAt = DateTime.Now,
            UserName = AuditTrail.NormalizeUserName(userName ?? userProvider?.CurrentUser),
            Action = Limit(action, 30),
            EntityName = Limit(entityName, 100),
            EntityId = LimitNullable(entityId, 200),
            Description = Limit(description, 500),
            Changes = changes,
            IpAddress = LimitNullable(userProvider?.IpAddress, 64),
            HttpMethod = LimitNullable(userProvider?.HttpMethod, 10),
            RequestPath = LimitNullable(userProvider?.RequestPath, 500)
        };

    private static Dictionary<string, object?> CollectChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name)) continue;

            if (SensitiveProperties.Contains(name))
            {
                if (entry.State != EntityState.Modified || property.IsModified)
                    changes[name] = "[Đã ẩn]";
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new { New = SafeValue(property.CurrentValue) };
                    break;
                case EntityState.Deleted:
                    changes[name] = new { Old = SafeValue(property.OriginalValue) };
                    break;
                case EntityState.Modified when property.IsModified &&
                                                   !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new
                    {
                        Old = SafeValue(property.OriginalValue),
                        New = SafeValue(property.CurrentValue)
                    };
                    break;
            }
        }

        return changes;
    }

    private static string? GetEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return null;

        var values = new List<string>();
        foreach (var keyProperty in key.Properties)
        {
            var property = entry.Property(keyProperty.Name);
            if (property.IsTemporary) return null;
            var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
            if (value is null || IsDefaultValue(value)) return null;
            values.Add(value.ToString()!);
        }

        return values.Count == 0 ? null : string.Join(",", values);
    }

    private static object? SafeValue(object? value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };

    private static bool IsDefaultValue(object value)
    {
        var type = value.GetType();
        return type.IsValueType && Equals(value, Activator.CreateInstance(type));
    }

    private static string ActionLabel(string action) => action switch
    {
        AuditLogActions.Create => "Tạo mới",
        AuditLogActions.Update => "Cập nhật",
        AuditLogActions.Delete => "Xóa",
        _ => action
    };

    private static string Limit(string value, int maxLength)
    {
        value = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? LimitNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
