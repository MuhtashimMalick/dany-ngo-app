using NgoFund.Application.Abstractions;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// <see cref="ICurrentUserService"/> for non-HTTP contexts (the migrator, background jobs) —
/// there is no request to attribute changes to, so every property is null and audit rows/
/// CreatedBy columns correctly read as system-initiated.
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public string? UserName => "system";
    public string? IpAddress => null;
    public string? MachineName => Environment.MachineName;
}
