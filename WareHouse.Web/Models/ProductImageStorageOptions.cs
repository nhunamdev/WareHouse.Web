namespace WareHouse.Web.Models;

public sealed class ProductImageStorageOptions
{
    public const string SectionName = "ProductImages";

    public string RootPath { get; set; } = "data/images";
    public string RequestPath { get; set; } = "/data/images";
    public int MaxFilesPerProduct { get; set; } = 15;
    public int MaxFileSizeMb { get; set; } = 5;
    public int MaxTotalSizeMb { get; set; } = 25;
}
