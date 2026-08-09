namespace NgoFund.Application.Abstractions;

public record GeneratedAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues signed access tokens. Permissions are embedded as claims at issue time (not looked up
/// per-request) — the tradeoff is that a permission change only takes effect on the user's next
/// token refresh, which the short access-token lifetime already bounds.
/// </summary>
public interface IJwtTokenService
{
    GeneratedAccessToken GenerateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles, IEnumerable<string> permissionCodes);

    string GenerateRefreshToken();
}
