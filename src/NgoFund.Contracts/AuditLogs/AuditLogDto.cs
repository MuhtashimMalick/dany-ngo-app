namespace NgoFund.Contracts.AuditLogs;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityName,
    string EntityId,
    IReadOnlyList<string>? ChangedColumns,
    DateTimeOffset OccurredAt);
