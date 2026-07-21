namespace WareHouse.Fontend.Models;

public sealed class StoreContactOptions
{
    public const string SectionName = "StoreContact";

    public string Phone { get; set; } = "0375.751.808";

    public string ZaloUrl { get; set; } = string.Empty;

    public string PhoneDigits => new(Phone.Where(char.IsDigit).ToArray());

    public string TelephoneUrl => $"tel:{PhoneDigits}";

    public string PhoneLabel => Phone.Trim();

    public string ZaloAddress => string.IsNullOrWhiteSpace(ZaloUrl)
        ? $"https://zalo.me/{PhoneDigits}"
        : ZaloUrl;
}
