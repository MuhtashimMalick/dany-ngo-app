using NgoFund.Contracts.Auth;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// Port for authentication. Implemented in Infrastructure (it needs
/// <c>UserManager&lt;ApplicationUser&gt;</c>, an Identity-framework type Application must not
/// depend on) — this interface only ever sees Contracts DTOs.
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
