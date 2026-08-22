namespace NgoFund.Contracts.Loans;

public record PreviewLoanScheduleRequest(
    Guid ApplicationId,
    int InstallmentCount,
    DateOnly FirstDueDate,
    string Frequency);
