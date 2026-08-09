namespace NgoFund.Contracts.Users;

public record UserSummaryDto(
    Guid Id,
    string Email,
    string FullName,
    string? Designation,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);
