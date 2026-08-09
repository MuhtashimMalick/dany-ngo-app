namespace NgoFund.Contracts.Users;

public record CreateUserRequest(
    string Email,
    string FullName,
    string? Designation,
    string TemporaryPassword,
    IReadOnlyList<string> Roles);
