using System.Security.Claims;
using NgoFund.Application.Abstractions;

namespace NgoFund.Api.Services;

/// <summary>
/// <see cref="ICurrentUserService"/> backed by the current HTTP request. Lives in Api (not
/// Infrastructure) because it depends on <see cref="IHttpContextAccessor"/>, an ASP.NET Core
/// hosting concern — Infrastructure only ever sees the interface.
/// </summary>
public sealed class HttpContextCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var value = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => Context?.User.FindFirstValue(ClaimTypes.Name) ?? Context?.User.Identity?.Name;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? MachineName => Environment.MachineName;
}
