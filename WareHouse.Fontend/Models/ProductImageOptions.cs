namespace WareHouse.Fontend.Models;

public sealed class ProductImageOptions
{
    public string RootPath { get; set; } = string.Empty;
    public string[] FallbackRootPaths { get; set; } = [];
    public bool DiscoverAdminSibling { get; set; } = true;
    public string RequestPath { get; set; } = "/data";
    public string? SourceBaseUrl { get; set; }
}
