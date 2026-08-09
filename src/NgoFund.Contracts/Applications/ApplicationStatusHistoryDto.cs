namespace NgoFund.Contracts.Applications;

public record ApplicationStatusHistoryDto(Guid Id, string? FromStatus, string ToStatus, string? Remarks, DateTimeOffset ChangedAt);
