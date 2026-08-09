using Microsoft.AspNetCore.Authorization;

namespace NgoFund.Api.Authorization;

/// <summary>
/// <c>[HasPermission("donations.create")]</c> — the permission code becomes the authorization
/// policy name, resolved dynamically by <see cref="PermissionPolicyProvider"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HasPermissionAttribute(string permissionCode) : AuthorizeAttribute(policy: permissionCode);
