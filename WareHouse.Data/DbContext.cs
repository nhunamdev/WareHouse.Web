using Microsoft.EntityFrameworkCore;

namespace WareHouse.Data;

public class WareHouseDbContext(
    DbContextOptions<WareHouseDbContext> options,
    IAuditUserProvider? auditUserProvider = null) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<StoreBanner> StoreBanners => Set<StoreBanner>();
    public DbSet<ProductAttribute> Attributes => Set<ProductAttribute>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemAttribute> ItemAttributes => Set<ItemAttribute>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseStock> WarehouseStocks => Set<WarehouseStock>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentDetail> StockDocumentDetails => Set<StockDocumentDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareAuditData();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareAuditData();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareAuditData()
    {
        AuditTrail.Apply(ChangeTracker, auditUserProvider);
        var logs = AuditLogTrail.CreateAutomaticLogs(ChangeTracker, auditUserProvider);
        if (logs.Count > 0) AuditLogs.AddRange(logs);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasIndex(x => new { x.ProductId, x.SortOrder });
            entity.HasIndex(x => new { x.ProductId, x.IsPrimary });
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StoreBanner>(entity =>
        {
            entity.HasIndex(x => new { x.IsActive, x.SortOrder });
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.ImagePath).IsRequired().HasMaxLength(300);
            entity.Property(x => x.Url).HasMaxLength(1000);
        });

        modelBuilder.Entity<ProductAttribute>(entity =>
        {
            entity.ToTable("Attributes");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AttributeValue>(entity =>
        {
            entity.HasIndex(x => new { x.AttributeId, x.Value }).IsUnique();
            entity.HasOne(x => x.Attribute)
                .WithMany(x => x.Values)
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemAttribute>(entity =>
        {
            entity.HasKey(x => new { x.ItemId, x.AttributeValueId });
            entity.HasOne(x => x.Item)
                .WithMany(x => x.ItemAttributes)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AttributeValue)
                .WithMany(x => x.ItemAttributes)
                .HasForeignKey(x => x.AttributeValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<WarehouseStock>(entity =>
        {
            entity.HasKey(x => new { x.WarehouseId, x.ItemId });
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne(x => x.Warehouse)
                .WithMany(x => x.Stocks)
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Item)
                .WithMany(x => x.WarehouseStocks)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Debt).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StockDocument>(entity =>
        {
            entity.HasIndex(x => x.DocumentNo).IsUnique();
            entity.HasIndex(x => new { x.DocumentType, x.Status, x.DocumentDate });
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.DebtAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FromWarehouse)
                .WithMany(x => x.FromDocuments)
                .HasForeignKey(x => x.FromWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToWarehouse)
                .WithMany(x => x.ToDocuments)
                .HasForeignKey(x => x.ToWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockDocumentDetail>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Document)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Item)
                .WithMany(x => x.StockDocumentDetails)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.Status, x.PaymentDate });
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Document)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.PaymentId, x.DocumentId }).IsUnique();
            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document)
                .WithMany(x => x.PaymentAllocations)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.UserName, x.OccurredAt });
            entity.HasIndex(x => new { x.EntityName, x.EntityId });
            entity.HasIndex(x => x.Action);
            entity.Property(x => x.Changes).HasColumnType("nvarchar(max)");
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(IAuditableEntity).IsAssignableFrom(x.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property(nameof(IAuditableEntity.CreatedBy))
                .HasMaxLength(AuditTrail.UserNameMaxLength)
                .IsRequired();
            entity.Property(nameof(IAuditableEntity.UpdatedBy))
                .HasMaxLength(AuditTrail.UserNameMaxLength);
        }

        modelBuilder.Entity<Warehouse>().HasData(
            new Warehouse
            {
                Id = 1, Code = "KHO-A", Name = "Kho A", IsActive = true,
                CreatedAt = new DateTime(2026, 7, 16), CreatedBy = AuditTrail.SystemUser
            },
            new Warehouse
            {
                Id = 2, Code = "KHO-B", Name = "Kho B", IsActive = true,
                CreatedAt = new DateTime(2026, 7, 16), CreatedBy = AuditTrail.SystemUser
            },
            new Warehouse
            {
                Id = 3, Code = "KHO-C", Name = "Kho C", IsActive = true,
                CreatedAt = new DateTime(2026, 7, 16), CreatedBy = AuditTrail.SystemUser
            });

        modelBuilder.Entity<Customer>().HasData(
            new Customer
            {
                Id = 1,
                Code = "KHACH-LE",
                Name = "Khách lẻ",
                CustomerType = CustomerType.Retail,
                Debt = 0,
                IsActive = true,
                CreatedAt = new DateTime(2026, 7, 16),
                CreatedBy = AuditTrail.SystemUser
            });
    }
}
