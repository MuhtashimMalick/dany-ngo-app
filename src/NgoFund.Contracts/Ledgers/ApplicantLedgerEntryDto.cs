namespace NgoFund.Contracts.Ledgers;

/// <summary>
/// One row in an applicant's chronological history. Voided entries are included (per D3, hiding
/// them would misrepresent the record) but carry their own <see cref="Status"/> so the reader can
/// tell — they are excluded from every total on <see cref="FundApplicantLedgerRowDto"/>, never
/// from this list.
/// </summary>
public record ApplicantLedgerEntryDto(
    DateOnly Date,
    /// <summary>"Disbursement" | "Repayment".</summary>
    string EntryType,
    string ReferenceNumber,
    Guid FundCategoryId,
    string FundCategoryName,
    string ApplicationNumber,
    decimal Amount,
    /// <summary>"Completed" | "Voided".</summary>
    string Status);
