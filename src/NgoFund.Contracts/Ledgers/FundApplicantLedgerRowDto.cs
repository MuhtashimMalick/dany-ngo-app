namespace NgoFund.Contracts.Ledgers;

/// <summary>
/// One applicant's summary against one fund — the per-fund summary row inside
/// <see cref="ApplicantLedgerDto"/>.
/// <see cref="TotalRepaid"/> is loan repayments, never donations — <c>Donor</c> has no link to
/// <c>Applicant</c> in this schema, so an applicant can never "contribute" a donation.
/// </summary>
public record FundApplicantLedgerRowDto(
    Guid ApplicantId,
    string FullName,
    string Cnic,
    string? MembershipNumber,
    Guid FundCategoryId,
    string FundCategoryName,
    decimal TotalReceived,
    decimal TotalRepaid,
    decimal OutstandingRecoverable,
    decimal WrittenOffAmount,
    /// <summary>"NotApplicable" | "Cleared" | "Outstanding" | "WrittenOff" — computed server-side.</summary>
    string RecoveryStatus,
    int ActivityCount,
    DateOnly? LastActivityDate);
