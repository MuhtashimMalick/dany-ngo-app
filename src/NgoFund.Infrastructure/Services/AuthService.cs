using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Auth;
using NgoFund.Contracts.Users;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Security;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Lives in Infrastructure (not Application) because it needs <see cref="UserManager{TUser}"/>,
/// which is generic over <see cref="ApplicationUser"/> — an Identity-framework type Application
/// must not depend on. Application only ever sees this through the <see cref="IAuthService"/>
/// port and Contracts DTOs.
/// </summary>
public class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new InvalidCredentialsException();

        if (!user.IsActive)
        {
            throw new UserInactiveException();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new AccountLockedOutException();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // AccessFailedAsync itself locks the account once MaxFailedAccessAttempts is reached
            // (Identity's own bookkeeping) — no manual lockout counting needed here.
            await userManager.AccessFailedAsync(user);
            throw new InvalidCredentialsException();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await IssueTokensAsync(user, ipAddress, replaces: null, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.RefreshToken);

        var existing = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existing is null || !existing.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        if (!existing.User.IsActive)
        {
            throw new UserInactiveException();
        }

        return await IssueTokensAsync(existing.User, ipAddress, replaces: existing, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHasher.Hash(request.RefreshToken);
        var existing = await dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException("User", userId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
            {
                throw new InvalidCredentialsException();
            }

            throw new PasswordPolicyViolationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, string? ipAddress, RefreshToken? replaces, CancellationToken cancellationToken)
    {
        var roleNames = await userManager.GetRolesAsync(user);

        var roleIds = await dbContext.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var permissionCodes = await dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(dbContext.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email!, user.FullName, roleNames, permissionCodes);
        var refreshTokenPlain = jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = TokenHasher.Hash(refreshTokenPlain);

        if (replaces is not null)
        {
            replaces.RevokedAt = DateTimeOffset.UtcNow;
            replaces.ReplacedByTokenHash = refreshTokenHash;
        }

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var userDto = new UserSummaryDto(
            user.Id, user.Email!, user.FullName, user.Designation, user.IsActive,
            user.MustChangePassword, user.LastLoginAt, roleNames.ToList());

        return new AuthResponse(accessToken.Token, accessToken.ExpiresAt, refreshTokenPlain, userDto);
    }
}
