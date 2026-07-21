using System.ComponentModel.DataAnnotations;
using WareHouse.Data;

namespace WareHouse.Web.ViewModels;

public class PaymentEditViewModel
{
    public long Id { get; set; }
    public int OriginalCustomerId { get; set; }
    public long? OriginalDocumentId { get; set; }
    public PaymentStatus OriginalStatus { get; set; } = PaymentStatus.Draft;
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn khách hàng.")]
    [Display(Name = "Khách hàng")]
    public int CustomerId { get; set; }
    [Display(Name = "Đơn bán")]
    public long? DocumentId { get; set; }
    [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
    [Display(Name = "Số tiền")]
    public decimal Amount { get; set; }
    [Display(Name = "Ngày thu")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    [Display(Name = "Phương thức")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Remark { get; set; }
    public IReadOnlyList<Customer> Customers { get; set; } = [];
    public IReadOnlyList<StockDocument> OutstandingSales { get; set; } = [];
    public List<PaymentAllocationEditViewModel> Allocations { get; set; } = [];
}

public class PaymentAllocationEditViewModel
{
    public long DocumentId { get; set; }
    public bool Selected { get; set; }
    public decimal Amount { get; set; }
    public DateTime DocumentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal AvailableDebt { get; set; }
}

public class CustomerDetailsViewModel
{
    public Customer Customer { get; set; } = null!;
    public IReadOnlyList<CustomerStatementRow> Statement { get; set; } = [];
    public IReadOnlyList<StockDocument> OutstandingSales { get; set; } = [];
}
