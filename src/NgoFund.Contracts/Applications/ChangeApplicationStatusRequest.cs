namespace NgoFund.Contracts.Applications;

public record ChangeApplicationStatusRequest(string NewStatus, string? Remarks, string? RejectionReason);
