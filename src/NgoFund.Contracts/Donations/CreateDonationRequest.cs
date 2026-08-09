namespace NgoFund.Contracts.Donations;

public record CreateDonationRequest(
    Guid DonorId,
    Guid FundCategoryId,
    decimal Amount,
    DateOnly DonationDate,
    string PaymentMethod,
    string? ReceiptReference,
    string? BankName,
    string? InstrumentNumber,
    string? Notes);
