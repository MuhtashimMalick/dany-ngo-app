namespace NgoFund.Contracts.Loans;

public record LoanRepaymentDto(
    Guid Id,
    string RepaymentNumber,
    Guid LoanAgreementId,
    decimal Amount,
    DateOnly RepaymentDate,
    string PaymentMethod,
    string? InstrumentNumber,
    string? BankName,
    string Status,
    string ReceivedFromName,
    string? ReceivedFromCnic,
    string? Notes,
    DateTimeOffset? VoidedAt,
    string? VoidReason);
