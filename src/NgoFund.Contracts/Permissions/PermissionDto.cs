namespace NgoFund.Contracts.Permissions;

public record PermissionDto(Guid Id, string Code, string Module, string DisplayName, string? Description);
