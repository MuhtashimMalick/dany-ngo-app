using NgoFund.Contracts.Permissions;
using NgoFund.Contracts.Roles;

namespace NgoFund.Application.Abstractions;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken);
}
