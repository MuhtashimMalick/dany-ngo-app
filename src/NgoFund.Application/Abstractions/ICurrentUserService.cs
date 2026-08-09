namespace NgoFund.Application.Abstractions;

/// <summary>
/// Minimal abstraction over "who is making this request", consumed by
/// <c>AuditSaveChangesInterceptor</c> to stamp CreatedBy/UpdatedBy and write audit log rows.
/// Implemented in the Api layer (it needs IHttpContextAccessor, an ASP.NET Core hosting
/// concern) — Infrastructure depends only on this interface, never on HttpContext directly.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
    string? MachineName { get; }
}
