using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using WareHouse.Data;

namespace WareHouse.Web.ViewModels;

public class ItemEditViewModel : IAuditableEntity
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm.")]
    [Display(Name = "Sản phẩm")]
    public int ProductId { get; set; }

    [StringLength(80)]
    [Display(Name = "Mã phân loại")]
    public string? Code { get; set; }

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá vốn không được âm.")]
    [Display(Name = "Giá vốn")]
    public decimal CostPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm.")]
    [Display(Name = "Giá bán")]
    public decimal SalePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<int> AttributeValueIds { get; set; } = [];

    public List<ItemBatchRowViewModel> Rows { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<Product> Products { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<ProductAttribute> AttributeGroups { get; set; } = [];
}

public class ItemBatchRowViewModel
{
    [Range(0, double.MaxValue, ErrorMessage = "Giá vốn không được âm.")]
    public decimal CostPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm.")]
    public decimal SalePrice { get; set; }

    public List<int> AttributeValueIds { get; set; } = [];
}
