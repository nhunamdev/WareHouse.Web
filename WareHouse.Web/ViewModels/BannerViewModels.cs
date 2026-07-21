using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WareHouse.Web.ViewModels;

public sealed class BannerEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề banner là bắt buộc.")]
    [StringLength(200)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "URL khi nhấn")]
    public string? Url { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    [Display(Name = "Đang hiển thị")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Hình ảnh")]
    public IFormFile? Image { get; set; }

    public string? ExistingImageUrl { get; set; }
}
