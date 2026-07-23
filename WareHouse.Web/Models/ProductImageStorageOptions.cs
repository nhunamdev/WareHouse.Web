namespace WareHouse.Web.Models;

public sealed class ProductImageStorageOptions
{
    public const string SectionName = "ProductImages";

    // Admin is the single owner of uploaded image files.
    public string RootPath { get; set; } = "data/images";
    public string RequestPath { get; set; } = "/data";
    public int MaxFilesPerProduct { get; set; } = 15;
    public int MaxFileSizeMb { get; set; } = 5;
    public int MaxTotalSizeMb { get; set; } = 25;
}
