using Microsoft.AspNetCore.Authorization;

namespace NgoFund.Api.Authorization;

public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
