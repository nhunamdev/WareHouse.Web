using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Http;
using WareHouse.Data;

namespace WareHouse.Web.ViewModels;

public sealed class ProductEditViewModel
{
    public Product Product { get; set; } = new();
    public List<ProductItemInput> Variants { get; set; } = [];
    public List<IFormFile> NewImages { get; set; } = [];
    public string? ImageOrder { get; set; }
    public string? PrimaryImageKey { get; set; }
    public string? RemovedImageIds { get; set; }
    public string? ImageAssignments { get; set; }

    [ValidateNever]
    public IReadOnlyList<ProductAttribute> AttributeGroups { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<ProductImage> ExistingImages { get; set; } = [];

    [ValidateNever]
    public string ImageBaseUrl { get; set; } = "/data";
}
