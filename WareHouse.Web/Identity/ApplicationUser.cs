using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using WareHouse.Data;

namespace WareHouse.Web.Identity;

public class ApplicationUser : IdentityUser, IAuditableEntity
{
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
