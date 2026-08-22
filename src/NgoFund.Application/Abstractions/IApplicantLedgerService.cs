using NgoFund.Contracts.Ledgers;

namespace NgoFund.Application.Abstractions;

public interface IApplicantLedgerService
{
    /// <summary>One applicant's full financial history, across every fund they've touched. Not paged — bounded by one person's history.</summary>
    Task<ApplicantLedgerDto> GetApplicantLedgerAsync(Guid applicantId, CancellationToken cancellationToken);
}
