namespace WareHouse.Fontend.Models;

using WareHouse.Fontend.Services;

public sealed record StoreCategoryDefinition(string Name, string Slug)
{
    public string DisplayName => StoreCulture.CategoryName(Name);
    public string Url => StoreCulture.Path($"/{Slug}");
}

public static class StoreCategories
{
    public static readonly IReadOnlyList<StoreCategoryDefinition> All =
    [
        new("Xe đạp", "xe-dap"),
        new("Xe điện", "xe-dien")
    ];

    public static StoreCategoryDefinition? FindBySlug(string? slug) => All.FirstOrDefault(x =>
        string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static StoreCategoryDefinition? FindByName(string? name) => All.FirstOrDefault(x =>
        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase));

    public static string UrlFor(string? name) => FindByName(name)?.Url ?? StoreCulture.Path("/");
}
