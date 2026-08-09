using Microsoft.AspNetCore.Authorization;

namespace NgoFund.Api.Authorization;

/// <summary>
/// Checks the "permission" claims embedded in the access token at login — no database round
/// trip per request. See <c>IJwtTokenService</c> for where those claims are populated.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
