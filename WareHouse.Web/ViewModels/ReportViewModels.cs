using WareHouse.Data;

namespace WareHouse.Web.ViewModels;

public class StockMovementReportViewModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? WarehouseId { get; set; }
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
    public IReadOnlyList<StockMovementRow> Rows { get; set; } = [];
}

public class DebtReportViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public IReadOnlyList<DebtReportRow> Rows { get; set; } = [];
}

public class SalesReportViewModel
{
    public PagedResult<StockDocument> Sales { get; set; } = new();
    public int OrderCount { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
}

public class PaginationViewModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Action { get; set; } = "Index";
    public Dictionary<string, string?> RouteValues { get; set; } = [];
}
