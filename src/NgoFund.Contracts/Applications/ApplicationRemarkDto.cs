namespace NgoFund.Contracts.Applications;

public record ApplicationRemarkDto(Guid Id, string Remark, bool IsInternal, DateTimeOffset CreatedAt);
