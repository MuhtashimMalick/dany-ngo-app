namespace NgoFund.Contracts.Payments;

public record CreatePaymentRequest(
    Guid ApplicationId,
    decimal Amount,
    DateOnly PaymentDate,
    string PaymentMethod,
    string? InstrumentNumber,
    string? BankName);
