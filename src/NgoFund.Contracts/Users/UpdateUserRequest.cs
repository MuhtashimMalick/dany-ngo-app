namespace NgoFund.Contracts.Users;

public record UpdateUserRequest(
    string FullName,
    string? Designation,
    bool IsActive,
    IReadOnlyList<string> Roles);
