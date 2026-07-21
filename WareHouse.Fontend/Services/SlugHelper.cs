using System.Globalization;
using System.Text;

namespace WareHouse.Fontend.Services;

public static class SlugHelper
{
    public static string ToSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "xe-dap";
        var normalized = value.Trim()
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        return slug.Trim('-') switch { { Length: > 0 } result => result, _ => "xe-dap" };
    }

    public static string ProductUrl(int id, string? name) => $"/{ToSlug(name)}-{id}";
}
