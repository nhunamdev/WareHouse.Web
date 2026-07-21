namespace WareHouse.Web.ViewModels;

public sealed class SortHeaderViewModel
{
    public required string Label { get; init; }
    public required string Column { get; init; }
    public string? CurrentSort { get; init; }
    public string? CurrentDirection { get; init; }
    public string? CssClass { get; init; }
}
