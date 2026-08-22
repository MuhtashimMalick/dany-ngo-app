namespace NgoFund.Contracts.Loans;

/// <summary>A General-fund application's interest-free (Qard al-Hasan) repayment plan. Zakat-fund
/// payments never have a loan agreement.</summary>
public record LoanAgreementDto(
    Guid Id,
    string LoanNumber,
    Guid ApplicationId,
    string ApplicationNumber,
    string ApplicantName,
    Guid FundCategoryId,
    string FundCategoryName,
    decimal PrincipalAmount,
    string Status,
    int InstallmentCount,
    /// <summary>Base installment amount; the final installment absorbs any rounding remainder.</summary>
    decimal InstallmentAmount,
    string Frequency,
    DateOnly FirstDueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    DateTimeOffset? WrittenOffAt,
    string? WrittenOffReason,
    string? Notes);
