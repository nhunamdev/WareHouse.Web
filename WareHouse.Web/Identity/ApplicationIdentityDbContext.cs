using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WareHouse.Data;

namespace WareHouse.Web.Identity;

public class ApplicationIdentityDbContext(
    DbContextOptions<ApplicationIdentityDbContext> options,
    IAuditUserProvider? auditUserProvider = null)
    : IdentityDbContext<ApplicationUser>(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AuditTrail.Apply(ChangeTracker, auditUserProvider);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AuditTrail.Apply(ChangeTracker, auditUserProvider);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.CreatedBy)
                .HasMaxLength(AuditTrail.UserNameMaxLength)
                .IsRequired();
            entity.Property(x => x.UpdatedBy)
                .HasMaxLength(AuditTrail.UserNameMaxLength);
        });
    }
}
