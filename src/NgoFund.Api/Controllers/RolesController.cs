using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Permissions;
using NgoFund.Contracts.Roles;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet("roles")]
    [HasPermission("roles.view")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
        => Ok(await roleService.GetRolesAsync(cancellationToken));

    [HttpGet("permissions")]
    [HasPermission("roles.view")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissions(CancellationToken cancellationToken)
        => Ok(await roleService.GetPermissionsAsync(cancellationToken));
}
