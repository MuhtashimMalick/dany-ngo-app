namespace NgoFund.Contracts.Donations;

public record DonationDto(
    Guid Id,
    string DonationNumber,
    Guid DonorId,
    string DonorName,
    Guid FundCategoryId,
    string FundCategoryName,
    decimal Amount,
    DateOnly DonationDate,
    string PaymentMethod,
    string? ReceiptReference,
    string? BankName,
    string? InstrumentNumber,
    string? Notes,
    string Status,
    DateTimeOffset? VoidedAt,
    string? VoidReason);
