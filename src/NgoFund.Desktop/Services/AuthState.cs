using System.IdentityModel.Tokens.Jwt;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Users;

namespace NgoFund.Desktop.Services;

/// <summary>
/// In-memory session state for this desktop process — one WPF window, one session, so a simple
/// singleton with a change notification is enough (no need for a Redux-style store). Permission
/// codes are read directly off the access token's claims rather than duplicated into
/// <see cref="UserSummaryDto"/>: the token is already the source of truth the API itself trusts,
/// and this is only ever used for UI show/hide decisions — every real permission check still
/// happens server-side on each request.
/// </summary>
public class AuthState
{
    private readonly HashSet<string> _permissions = [];

    public UserSummaryDto? CurrentUser { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null && AccessToken is not null;

    public event Action? Changed;

    public void SetSession(AuthResponse response)
    {
        CurrentUser = response.User;
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAt = response.AccessTokenExpiresAt;
        ExtractPermissions(response.AccessToken);
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentUser = null;
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = null;
        _permissions.Clear();
        Changed?.Invoke();
    }

    public bool HasPermission(string code) => _permissions.Contains(code);

    public void MarkPasswordChanged()
    {
        if (CurrentUser is not null)
        {
            CurrentUser = CurrentUser with { MustChangePassword = false };
            Changed?.Invoke();
        }
    }

    private void ExtractPermissions(string accessToken)
    {
        _permissions.Clear();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        foreach (var claim in token.Claims.Where(c => c.Type == "permission"))
        {
            _permissions.Add(claim.Value);
        }
    }
}
