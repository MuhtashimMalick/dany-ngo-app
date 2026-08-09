namespace NgoFund.Contracts.Payments;

public record PaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid ApplicationId,
    string ApplicationNumber,
    string ApplicantName,
    Guid FundCategoryId,
    string FundCategoryName,
    decimal Amount,
    DateOnly PaymentDate,
    string PaymentMethod,
    string? InstrumentNumber,
    string? BankName,
    string? PaidToName,
    string? PaidToCnic,
    string? PaidToRelation,
    string? Remarks,
    string Status,
    DateTimeOffset? VoidedAt,
    string? VoidReason);
