using System.ComponentModel.DataAnnotations;
using WareHouse.Data;

namespace WareHouse.Web.ViewModels;

public class DocumentEditViewModel
{
    public long Id { get; set; }
    public string? DocumentNo { get; set; }
    public StockDocumentType DocumentType { get; set; }

    [Display(Name = "Ngày chứng từ")]
    public DateTime DocumentDate { get; set; } = DateTime.Now;

    [Display(Name = "Khách hàng")]
    public int? CustomerId { get; set; }

    [StringLength(30, ErrorMessage = "Số điện thoại không được vượt quá 30 ký tự.")]
    [Display(Name = "Số điện thoại khách")]
    public string? CustomerPhone { get; set; }

    [Display(Name = "Kho nguồn")]
    public int? FromWarehouseId { get; set; }

    [Display(Name = "Kho đích")]
    public int? ToWarehouseId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Số tiền đã trả không được âm.")]
    [Display(Name = "Đã thanh toán")]
    public decimal PaidAmount { get; set; }

    [StringLength(1000)]
    [Display(Name = "Ghi chú")]
    public string? Remark { get; set; }
    public List<DocumentLineViewModel> Details { get; set; } = [new()];
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
    public IReadOnlyList<Customer> Customers { get; set; } = [];
    public IReadOnlyList<ItemSelection> Items { get; set; } = [];

    public DocumentInput ToInput() => new()
    {
        Id = Id,
        DocumentType = DocumentType,
        DocumentDate = DocumentDate,
        CustomerId = CustomerId,
        CustomerPhone = CustomerPhone,
        FromWarehouseId = FromWarehouseId,
        ToWarehouseId = ToWarehouseId,
        PaidAmount = 0,
        Remark = Remark,
        Details = Details.Where(x => x.ItemId > 0).Select(x => new DocumentDetailInput
        {
            ItemId = x.ItemId,
            Quantity = x.Quantity,
            Price = x.Price
        }).ToList()
    };
}

public class DocumentLineViewModel
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal Price { get; set; }
}

public class InventoryViewModel
{
    public StockMatrix Matrix { get; set; } = new();
    public IReadOnlyList<InventoryProductGroupViewModel> ProductGroups { get; set; } = [];
    public InventorySummaryViewModel Summary { get; set; } = new();
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public string? Keyword { get; set; }
    public IReadOnlyList<Product> Products { get; set; } = [];
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
}

public class InventoryProductGroupViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<StockMatrixRow> Variants { get; set; } = [];
    public int VariantCount => Variants.Count;
    public decimal TotalQuantity => Variants.Sum(x => x.TotalQuantity);
    public decimal InventoryValue => Variants.Sum(x => x.InventoryValue);
    public bool HasNegativeStock => Variants.Any(x => x.Quantities.Values.Any(quantity => quantity < 0));
    public decimal MissingQuantity => Variants.Sum(x =>
        x.Quantities.Values.Where(quantity => quantity < 0).Sum(quantity => Math.Abs(quantity)));
}

public class InventorySummaryViewModel
{
    public decimal TotalQuantity { get; set; }
    public decimal InventoryValue { get; set; }
    public int ProductCount { get; set; }
    public int InStockProductCount { get; set; }
    public int OutOfStockProductCount { get; set; }
    public int NegativeStockProductCount { get; set; }
    public decimal MissingQuantity { get; set; }
}

public class BackorderViewModel
{
    public int? WarehouseId { get; set; }
    public string? Keyword { get; set; }
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
    public IReadOnlyList<NegativeStockRow> Rows { get; set; } = [];
    public int ProductCount => Rows.Select(x => x.ProductId).Distinct().Count();
    public decimal MissingQuantity => Rows.Sum(x => x.MissingQuantity);
    public decimal EstimatedRestockCost => Rows.Sum(x => x.EstimatedRestockCost);
}
