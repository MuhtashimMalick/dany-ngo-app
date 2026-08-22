namespace NgoFund.Contracts.Loans;

/// <summary>Deliberately has no principal field — principal is always read server-side from the
/// application's approved amount, never accepted from the client.</summary>
public record CreateLoanAgreementRequest(
    Guid ApplicationId,
    int InstallmentCount,
    DateOnly FirstDueDate,
    string Frequency,
    string? Notes);
