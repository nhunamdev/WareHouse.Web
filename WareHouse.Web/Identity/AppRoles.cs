namespace WareHouse.Web.Identity;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Sale = "Sale";

    public const string AdminOrManager = Admin + "," + Manager;
    public const string SalesAccess = Admin + "," + Manager + "," + Sale;

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Sale];

    public static string DisplayName(string role) => role switch
    {
        Admin => "Quản trị viên",
        Manager => "Quản lý",
        Sale => "Nhân viên bán hàng",
        _ => role
    };
}
