using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Ledgers;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// The chronological, running-balance ledger for one fund (client feedback: Date / Category /
/// No. / Name / GRN / OG / Total). A direct projection of <c>fund_transactions</c> in full — never
/// a UNION of <c>donations</c>/<c>payments</c>, which would silently drop <c>Adjustment</c>,
/// <c>OpeningBalance</c>, <c>LoanRepayment</c>, and every reversal type and make
/// <see cref="FundTransactionLedgerRowDto.RunningBalance"/> drift from
/// <c>vw_fund_balances.balance</c>. The label joins below are purely cosmetic (LEFT JOINs outward
/// from <c>fund_transactions</c>) — they never filter which rows appear.
/// </summary>
public class FundTransactionLedgerService(AppDbContext dbContext) : IFundTransactionLedgerService
{
    public async Task<PagedResult<FundTransactionLedgerRowDto>> GetFundTransactionLedgerAsync(
        Guid fundCategoryId, PagedQuery query, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pattern = ToPattern(query.Search);

        var totalCount = await GetTotalCountAsync(fundCategoryId, fromDate, toDate, pattern, cancellationToken);
        var rows = await QueryRowsAsync(fundCategoryId, fromDate, toDate, pattern, (page - 1) * pageSize, pageSize, cancellationToken);

        return new PagedResult<FundTransactionLedgerRowDto>(rows, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<FundTransactionLedgerRowDto>> GetFullFundTransactionLedgerAsync(
        Guid fundCategoryId, DateOnly? fromDate, DateOnly? toDate, string? search, CancellationToken cancellationToken)
        => await QueryRowsAsync(fundCategoryId, fromDate, toDate, ToPattern(search), offset: null, limit: null, cancellationToken);

    private static string? ToPattern(string? search) => string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

    // GetTotalCountAsync and QueryRowsAsync both build on the identical full_history CTE (verbatim
    // — same joins, same column list) and the identical outer WHERE (date range + search). EF's
    // SqlQuery interpolation turns every {} hole into a bound parameter, not raw SQL text, so the
    // CTE can't be factored into a shared C# string; it's duplicated on purpose, the same way
    // ApplicantLedgerService duplicates its "pairs"/"ledger" CTEs between row and count queries.
    // QueryRowsAsync itself IS shared — parameterized by nullable offset/limit — between the
    // paginated ledger (GetFundTransactionLedgerAsync, real offset/pageSize) and the full
    // unpaginated export (GetFullFundTransactionLedgerAsync, offset/limit both null, which
    // PostgreSQL treats as OFFSET 0 / LIMIT ALL). If GetTotalCountAsync's WHERE ever drifts from
    // QueryRowsAsync's, paging will report a TotalCount that doesn't match what the rows query
    // actually returns — keep them in lockstep.
    private async Task<int> GetTotalCountAsync(Guid fundCategoryId, DateOnly? fromDate, DateOnly? toDate, string? pattern, CancellationToken cancellationToken)
    {
        return await dbContext.Database.SqlQuery<int>(
            $"""
            WITH full_history AS (
                SELECT
                    ft.id,
                    ft.transaction_date,
                    CASE
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN app.application_number
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN app2.application_number
                        ELSE NULL
                    END AS case_number,
                    CASE
                        WHEN ft.reference_type IN ('Donation', 'DonationReversal') THEN d.donation_number
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN p.payment_number
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN lr.repayment_number
                        ELSE ft.id::text
                    END AS reference_number,
                    CASE
                        WHEN ft.reference_type IN ('Donation', 'DonationReversal') THEN 'Received from ' || don.full_name
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN a.full_name
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN a2.full_name
                        ELSE COALESCE(NULLIF(ft.description, ''), ft.reference_type)
                    END AS party_name
                FROM fund_transactions ft
                LEFT JOIN donations d ON ft.reference_type IN ('Donation', 'DonationReversal') AND d.id = ft.reference_id
                LEFT JOIN donors don ON don.id = d.donor_id
                LEFT JOIN payments p ON ft.reference_type IN ('Payment', 'PaymentReversal') AND p.id = ft.reference_id
                LEFT JOIN applications app ON app.id = p.application_id
                LEFT JOIN applicants a ON a.id = app.applicant_id
                LEFT JOIN loan_repayments lr ON ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') AND lr.id = ft.reference_id
                LEFT JOIN loan_agreements la ON la.id = lr.loan_agreement_id
                LEFT JOIN applications app2 ON app2.id = la.application_id
                LEFT JOIN applicants a2 ON a2.id = app2.applicant_id
                WHERE ft.fund_category_id = {fundCategoryId}
            )
            SELECT COUNT(*)::int AS "Value"
            FROM full_history
            WHERE ({fromDate}::date IS NULL OR transaction_date >= {fromDate})
              AND ({toDate}::date IS NULL OR transaction_date <= {toDate})
              AND ({pattern}::text IS NULL
                  OR party_name ILIKE {pattern}
                  OR case_number ILIKE {pattern}
                  OR reference_number ILIKE {pattern})
            """).SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Rows for either the paginated ledger or the full export. <paramref name="offset"/>/
    /// <paramref name="limit"/> null means "no pagination" — PostgreSQL treats <c>OFFSET NULL</c>
    /// as <c>OFFSET 0</c> and <c>LIMIT NULL</c> as <c>LIMIT ALL</c>, so the one query serves both
    /// callers. The explicit <c>::bigint</c> casts are needed because Npgsql can't infer a
    /// parameter type from a C# <c>int?</c> that might be null.
    /// </summary>
    private async Task<List<FundTransactionLedgerRowDto>> QueryRowsAsync(
        Guid fundCategoryId, DateOnly? fromDate, DateOnly? toDate, string? pattern, int? offset, int? limit, CancellationToken cancellationToken)
    {
        return await dbContext.Database.SqlQuery<FundTransactionLedgerRowDto>(
            $"""
            WITH full_history AS (
                -- The running balance MUST be computed here, over the fund's entire history,
                -- before any date filter or pagination — otherwise it stops being the fund's
                -- actual balance and becomes a meaningless partial sum (see IFundTransactionLedgerService).
                SELECT
                    ft.id,
                    ft.transaction_date,
                    ft.reference_type,
                    ft.direction,
                    ft.amount,
                    ft.description,
                    CASE
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN ac.name
                        ELSE NULL
                    END AS category_name,
                    CASE
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN app.application_number
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN app2.application_number
                        ELSE NULL
                    END AS case_number,
                    CASE
                        WHEN ft.reference_type IN ('Donation', 'DonationReversal') THEN d.donation_number
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN p.payment_number
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN lr.repayment_number
                        ELSE ft.id::text
                    END AS reference_number,
                    CASE
                        WHEN ft.reference_type IN ('Donation', 'DonationReversal') THEN 'Received from ' || don.full_name
                        WHEN ft.reference_type IN ('Payment', 'PaymentReversal') THEN a.full_name
                        WHEN ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') THEN a2.full_name
                        ELSE COALESCE(NULLIF(ft.description, ''), ft.reference_type)
                    END AS party_name,
                    SUM(CASE WHEN ft.direction = 'Credit' THEN ft.amount ELSE -ft.amount END)
                        OVER (ORDER BY ft.transaction_date, ft.id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_balance
                FROM fund_transactions ft
                LEFT JOIN donations d ON ft.reference_type IN ('Donation', 'DonationReversal') AND d.id = ft.reference_id
                LEFT JOIN donors don ON don.id = d.donor_id
                LEFT JOIN payments p ON ft.reference_type IN ('Payment', 'PaymentReversal') AND p.id = ft.reference_id
                LEFT JOIN applications app ON app.id = p.application_id
                LEFT JOIN applicants a ON a.id = app.applicant_id
                LEFT JOIN application_categories ac ON ac.id = app.application_category_id
                LEFT JOIN loan_repayments lr ON ft.reference_type IN ('LoanRepayment', 'LoanRepaymentReversal') AND lr.id = ft.reference_id
                LEFT JOIN loan_agreements la ON la.id = lr.loan_agreement_id
                LEFT JOIN applications app2 ON app2.id = la.application_id
                LEFT JOIN applicants a2 ON a2.id = app2.applicant_id
                WHERE ft.fund_category_id = {fundCategoryId}
            )
            SELECT
                transaction_date,
                reference_type,
                category_name,
                case_number,
                reference_number,
                party_name,
                CASE WHEN direction = 'Credit' THEN amount ELSE NULL END AS amount_in,
                CASE WHEN direction = 'Debit' THEN amount ELSE NULL END AS amount_out,
                running_balance,
                description
            FROM full_history
            WHERE ({fromDate}::date IS NULL OR transaction_date >= {fromDate})
              AND ({toDate}::date IS NULL OR transaction_date <= {toDate})
              AND ({pattern}::text IS NULL
                  OR party_name ILIKE {pattern}
                  OR case_number ILIKE {pattern}
                  OR reference_number ILIKE {pattern})
            ORDER BY transaction_date, id
            OFFSET {offset}::bigint
            LIMIT {limit}::bigint
            """).ToListAsync(cancellationToken);
    }
}
