using Microsoft.AspNetCore.Authorization;

namespace NgoFund.Api.Authorization;

/// <summary>
/// <c>[HasPermission("donations.create")]</c> — the permission code becomes the authorization
/// policy name, resolved dynamically by <see cref="PermissionPolicyProvider"/>. <c>AllowMultiple
/// = true</c> lets an endpoint stack two of these (e.g. <c>payments.view</c> +
/// <c>loans.view</c>) to require both — ASP.NET Core ANDs multiple <see cref="AuthorizeAttribute"/>s
/// on the same target, so this is least-privilege composition with no new permission and no seed
/// migration. Without this override the attribute would fall back to <see cref="AuthorizeAttribute"/>'s
/// own <c>AttributeUsage</c>, which is also <c>AllowMultiple = true</c> — but a derived attribute
/// that declares its own <see cref="AttributeUsageAttribute"/> does not inherit the base one, so
/// this has to be stated explicitly here rather than assumed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string permissionCode) : AuthorizeAttribute(policy: permissionCode);
