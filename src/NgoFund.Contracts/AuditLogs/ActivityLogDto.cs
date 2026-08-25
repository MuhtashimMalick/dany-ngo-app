namespace NgoFund.Contracts.AuditLogs;

/// <summary>One row in the client-facing activity feed — a human sentence, not raw EF/GUID detail.</summary>
public record ActivityLogDto(Guid Id, string Summary, string? UserName, DateTimeOffset OccurredAt);
