namespace NgoFund.Contracts.Ledgers;

/// <summary>One applicant's complete financial history across all funds they've touched.</summary>
public record ApplicantLedgerDto(
    Guid ApplicantId,
    string FullName,
    string Cnic,
    string? MembershipNumber,
    IReadOnlyList<FundApplicantLedgerRowDto> FundSummaries,
    IReadOnlyList<ApplicantLedgerEntryDto> Entries);
