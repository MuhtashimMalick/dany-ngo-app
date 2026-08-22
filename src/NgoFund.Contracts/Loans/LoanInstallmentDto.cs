namespace NgoFund.Contracts.Loans;

/// <summary>An installment's schedule fields are stored; <c>AmountAllocated</c>, <c>AmountRemaining</c>,
/// and <c>Status</c> are derived server-side from repayments and disbursed principal — never persisted.</summary>
public record LoanInstallmentDto(
    Guid Id,
    int SequenceNumber,
    decimal AmountDue,
    DateOnly DueDate,
    decimal AmountAllocated,
    decimal AmountRemaining,
    string Status);
