using NgoFund.Contracts.Users;

namespace NgoFund.Contracts.Auth;

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserSummaryDto User);
