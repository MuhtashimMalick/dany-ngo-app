namespace NgoFund.Contracts.Roles;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> PermissionCodes);
