using WareHouse.Data;

namespace WareHouse.Web.Identity;

public sealed class HttpAuditUserProvider(IHttpContextAccessor httpContextAccessor)
    : IAuditUserProvider
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public string CurrentUser =>
        HttpContext?.User.Identity?.Name ?? AuditTrail.SystemUser;

    public string? IpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? HttpMethod => HttpContext?.Request.Method;
    public string? RequestPath => HttpContext?.Request.Path.Value;
}
