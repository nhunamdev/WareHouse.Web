namespace WareHouse.Fontend.Models;

public sealed record StoreCategoryDefinition(string Name, string Slug)
{
    public string Url => $"/{Slug}";
}

public static class StoreCategories
{
    public static readonly IReadOnlyList<StoreCategoryDefinition> All =
    [
        new("Xe điện", "xe-dien"),
        new("Xe đạp", "xe-dap"),
        new("Xe đồ chơi", "xe-do-choi"),
        new("Khác", "khac")
    ];

    public static StoreCategoryDefinition? FindBySlug(string? slug) => All.FirstOrDefault(x =>
        string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static StoreCategoryDefinition? FindByName(string? name) => All.FirstOrDefault(x =>
        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    public static string UrlFor(string? name) => FindByName(name)?.Url ?? "/";
}
