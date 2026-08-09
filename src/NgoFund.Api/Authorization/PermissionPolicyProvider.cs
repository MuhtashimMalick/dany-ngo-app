using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace NgoFund.Api.Authorization;

/// <summary>
/// Turns every policy name into a permission requirement on demand — adding a new permission
/// (see <c>PermissionSeed</c>) never requires registering a matching named policy here. This is
/// what makes <c>[HasPermission("donations.create")]</c> work without any per-permission setup.
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
