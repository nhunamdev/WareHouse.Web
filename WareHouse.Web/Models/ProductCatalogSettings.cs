namespace WareHouse.Web.Models;

public sealed class ProductCatalogSettings
{
    public const string SectionName = "ProductCatalog";

    public List<string> Categories { get; set; } = [];

    public List<string> Units { get; set; } = [];
}
