using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Permissions;
using NgoFund.Contracts.Roles;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class RoleService(AppDbContext dbContext) : IRoleService
{
    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);

        var rolePermissionCodes = await dbContext.RolePermissions
            .Join(dbContext.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => new { rp.RoleId, p.Code })
            .ToListAsync(cancellationToken);

        return roles
            .Select(r => new RoleDto(
                r.Id,
                r.Name!,
                r.Description,
                r.IsSystem,
                rolePermissionCodes.Where(rp => rp.RoleId == r.Id).Select(rp => rp.Code).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Module, p.DisplayName, p.Description))
            .ToListAsync(cancellationToken);
    }
}
