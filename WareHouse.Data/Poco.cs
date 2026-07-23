using System.ComponentModel.DataAnnotations;

namespace WareHouse.Data;

public class Product : AuditableEntity
{
    public int Id { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? NameEn { get; set; }

    [StringLength(200)]
    public string? NameDe { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Đoạn mô tả ngắn dùng cho thẻ sản phẩm và trang danh sách.</summary>
    [StringLength(500)]
    public string? ShortDescription { get; set; }

    [StringLength(500)]
    public string? ShortDescriptionEn { get; set; }

    [StringLength(500)]
    public string? ShortDescriptionDe { get; set; }

    /// <summary>Nội dung chi tiết hiển thị ở trang sản phẩm.</summary>
    public string? DetailContent { get; set; }

    public string? DetailContentEn { get; set; }

    public string? DetailContentDe { get; set; }

    [StringLength(200)]
    public string? SeoTitle { get; set; }

    [StringLength(200)]
    public string? SeoTitleEn { get; set; }

    [StringLength(200)]
    public string? SeoTitleDe { get; set; }

    [StringLength(320)]
    public string? SeoDescription { get; set; }

    [StringLength(320)]
    public string? SeoDescriptionEn { get; set; }

    [StringLength(320)]
    public string? SeoDescriptionDe { get; set; }

    [StringLength(500)]
    public string? SeoKeywords { get; set; }

    [StringLength(500)]
    public string? SeoKeywordsEn { get; set; }

    [StringLength(500)]
    public string? SeoKeywordsDe { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<Item> Items { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];
}

public class ProductImage : AuditableEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? ItemId { get; set; }
    public int? ColorValueId { get; set; }

    [Required, StringLength(300)]
    public string RelativePath { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
    public Item? Item { get; set; }
    public AttributeValue? ColorValue { get; set; }
    public ICollection<ProductImageItem> ItemAssignments { get; set; } = [];
}

/// <summary>
/// Liên kết nhiều-nhiều giữa một ảnh và các phân loại cụ thể của sản phẩm.
/// Ví dụ cùng một ảnh có thể dùng cho Xanh/24 và Xanh/26.
/// </summary>
public class ProductImageItem
{
    public int ProductImageId { get; set; }
    public int ItemId { get; set; }
    public ProductImage ProductImage { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

/// <summary>
/// Banner hiển thị trên khu vực spotlight của website bán hàng.
/// </summary>
public class StoreBanner : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề banner là bắt buộc.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(200)]
    public string? TitleEn { get; set; }

    [StringLength(200)]
    public string? TitleDe { get; set; }

    [Required, StringLength(300)]
    public string ImagePath { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Url { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class ProductAttribute : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên thuộc tính là bắt buộc.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public ICollection<AttributeValue> Values { get; set; } = [];
}

public class AttributeValue : AuditableEntity
{
    public int Id { get; set; }
    public int AttributeId { get; set; }

    [Required(ErrorMessage = "Giá trị thuộc tính là bắt buộc.")]
    [StringLength(100)]
    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ProductAttribute Attribute { get; set; } = null!;
    public ICollection<ItemAttribute> ItemAttributes { get; set; } = [];
}

public class Item : AuditableEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Mã phân loại là bắt buộc.")]
    [StringLength(80)]
    public string Code { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá vốn không được âm.")]
    public decimal CostPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm.")]
    public decimal SalePrice { get; set; }

    public bool IsActive { get; set; } = true;
    public Product Product { get; set; } = null!;
    public ICollection<ItemAttribute> ItemAttributes { get; set; } = [];
    public ICollection<WarehouseStock> WarehouseStocks { get; set; } = [];
    public ICollection<StockDocumentDetail> StockDocumentDetails { get; set; } = [];
    public ICollection<ProductImageItem> ImageAssignments { get; set; } = [];
}

public class ItemAttribute : AuditableEntity
{
    public int ItemId { get; set; }
    public int AttributeValueId { get; set; }
    public Item Item { get; set; } = null!;
    public AttributeValue AttributeValue { get; set; } = null!;
}

public class Warehouse : AuditableEntity
{
    public int Id { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Tên kho là bắt buộc.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<WarehouseStock> Stocks { get; set; } = [];
    public ICollection<StockDocument> FromDocuments { get; set; } = [];
    public ICollection<StockDocument> ToDocuments { get; set; } = [];
}

public class WarehouseStock : AuditableEntity
{
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public Warehouse Warehouse { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public class Customer : AuditableEntity
{
    public int Id { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Tên khách hàng là bắt buộc.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }

    public decimal Debt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<StockDocument> Documents { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class StockDocument : AuditableEntity
{
    public long Id { get; set; }

    [Required]
    [StringLength(50)]
    public string DocumentNo { get; set; } = string.Empty;

    public StockDocumentType DocumentType { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }

    [StringLength(30)]
    public string? CustomerPhone { get; set; }

    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PreviousDebtAmount { get; set; }
    public decimal PreviousDebtPaidAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? FromWarehouse { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    public ICollection<StockDocumentDetail> Details { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = [];
}

public class StockDocumentDetail : AuditableEntity
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public StockDocument Document { get; set; } = null!;
    public Item Item { get; set; } = null!;
}

public class Payment : AuditableEntity
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public long? DocumentId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Draft;

    [StringLength(500)]
    public string? Remark { get; set; }

    public Customer Customer { get; set; } = null!;
    public StockDocument? Document { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = [];
}

public class PaymentAllocation : AuditableEntity
{
    public long Id { get; set; }
    public long PaymentId { get; set; }
    public long DocumentId { get; set; }
    public decimal Amount { get; set; }
    public Payment Payment { get; set; } = null!;
    public StockDocument Document { get; set; } = null!;
}

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? Id { get; set; }

    public static ServiceResult Ok(string message, long? id = null) =>
        new() { Success = true, Message = message, Id = id };

    public static ServiceResult Fail(string message) =>
        new() { Success = false, Message = message };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public class DocumentDetailInput
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public class DocumentInput
{
    public long Id { get; set; }
    public StockDocumentType DocumentType { get; set; }
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }
    public string? CustomerPhone { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Remark { get; set; }
    public List<DocumentDetailInput> Details { get; set; } = [];
}

public class ItemRow
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string Attributes { get; set; } = string.Empty;
    public decimal TotalStock { get; set; }
    public bool IsActive { get; set; }
}

public class ProductItemInput
{
    public int Id { get; set; }
    public string ClientKey { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public List<int> AttributeValueIds { get; set; } = [];
}

public class ItemSelection
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Stock { get; set; }
    public string? ImagePath { get; set; }
}

public class StockMatrix
{
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
    public IReadOnlyList<StockMatrixRow> Rows { get; set; } = [];
}

public class StockMatrixRow
{
    public int ItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCategory { get; set; }
    public string? ProductUnit { get; set; }
    public DateTime ProductCreatedAt { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public Dictionary<int, decimal> Quantities { get; set; } = [];
    public decimal TotalQuantity => Quantities.Values.Sum();
    public decimal InventoryValue => TotalQuantity * CostPrice;
}

public class NegativeStockRow
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductUnit { get; set; }
    public string Attributes { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal MissingQuantity => Math.Abs(Quantity);
    public decimal CostPrice { get; set; }
    public decimal EstimatedRestockCost => MissingQuantity * CostPrice;
    public DateTime? LastSaleAt { get; set; }
    public int SaleOrderCount { get; set; }
}

public class DashboardData
{
    public decimal TotalQuantity { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal ImportQuantity { get; set; }
    public decimal ExportQuantity { get; set; }
    public decimal SalesRevenue { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal CustomerDebt { get; set; }
    public int LowStockCount { get; set; }
    public IReadOnlyList<TopSellingRow> TopSelling { get; set; } = [];
    public IReadOnlyList<WarehouseStock> LowStocks { get; set; } = [];
    public IReadOnlyList<StockDocument> RecentDocuments { get; set; } = [];
}

public class TopSellingRow
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class StockMovementRow
{
    public int ItemId { get; set; }
    public int ProductId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public decimal ImportQuantity { get; set; }
    public decimal ExportQuantity { get; set; }
    public decimal TransferIn { get; set; }
    public decimal TransferOut { get; set; }
    public decimal SaleQuantity { get; set; }
    public decimal ReturnQuantity { get; set; }
    public decimal AdjustQuantity { get; set; }
    public decimal ClosingQuantity { get; set; }
}

public class CustomerStatementRow
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class DebtReportRow
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal CurrentDebt { get; set; }
    public int OutstandingSaleCount { get; set; }
    public decimal OutstandingSaleAmount { get; set; }
    public decimal PaymentInPeriod { get; set; }
}

public class DebtCustomerRow
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal CurrentDebt { get; set; }
    public int OutstandingSaleCount { get; set; }
    public DateTime? OldestDebtDate { get; set; }
    public DateTime? LatestDebtDate { get; set; }
    public IReadOnlyList<StockDocument> OutstandingSales { get; set; } = [];
}
