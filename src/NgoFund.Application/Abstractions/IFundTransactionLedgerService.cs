using NgoFund.Contracts.Common;
using NgoFund.Contracts.Ledgers;

namespace NgoFund.Application.Abstractions;

/// <summary>
/// A separate interface from <see cref="IApplicantLedgerService"/> on purpose (Interface
/// Segregation): that service's two methods are per-applicant aggregations over
/// applications/payments/loans, while this is a per-fund, row-per-event feed directly over
/// <c>fund_transactions</c> — a different read model with different collaborators.
/// </summary>
public interface IFundTransactionLedgerService
{
    /// <summary>The chronological, running-balance ledger for one fund (client feedback: Date /
    /// Category / No. / Name / GRN / OG / Total). The running balance is computed over the fund's
    /// entire history before any date filter or pagination is applied, so it always matches
    /// <c>vw_fund_balances.balance</c> on the last row and stays correct on every page.
    /// <paramref name="query"/>'s <c>Search</c> is honoured (case-insensitive, matches
    /// <c>party_name</c>/<c>case_number</c>/<c>reference_number</c>) — like the date filters, it is
    /// applied AFTER the running balance is computed, so a searched row's <c>RunningBalance</c> stays
    /// the fund's true balance at that point in history, never a running total recomputed over just
    /// the filtered subset.</summary>
    Task<PagedResult<FundTransactionLedgerRowDto>> GetFundTransactionLedgerAsync(
        Guid fundCategoryId, PagedQuery query, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
}
