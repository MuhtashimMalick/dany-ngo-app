using NgoFund.Domain.Entities;

namespace NgoFund.Infrastructure.Identity;

/// <summary>
/// Join entity making RBAC editable per role. Lives in Infrastructure (not Domain) because it
/// links to <see cref="ApplicationRole"/>, which is an Identity-framework type.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
