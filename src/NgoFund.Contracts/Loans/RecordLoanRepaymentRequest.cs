namespace NgoFund.Contracts.Loans;

public record RecordLoanRepaymentRequest(
    Guid LoanAgreementId,
    decimal Amount,
    DateOnly RepaymentDate,
    string PaymentMethod,
    string? InstrumentNumber,
    string? BankName,
    string ReceivedFromName,
    string? ReceivedFromCnic,
    string? Notes);
