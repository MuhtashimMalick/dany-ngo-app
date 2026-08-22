using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Ledgers;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Read-side aggregation only — no new tables, no new <c>fund_transactions</c> rows, nothing that
/// touches the append-only ledger. Every figure is derived at read time from
/// <c>applications</c>/<c>payments</c>/<c>loan_agreements</c>/<c>loan_repayments</c> and
/// <c>vw_loan_balances</c>, the same way <see cref="FundCategoryService"/> derives fund balances
/// from <c>vw_fund_balances</c> rather than storing one.
///
/// "Paid in" for an applicant means loan repayments, never donations: <c>Donor</c> has no FK to
/// <c>Applicant</c> anywhere in this schema, so there is no join path from an applicant to a
/// donation to attribute one to them.
/// </summary>
public class ApplicantLedgerService(AppDbContext dbContext) : IApplicantLedgerService
{
    public async Task<ApplicantLedgerDto> GetApplicantLedgerAsync(Guid applicantId, CancellationToken cancellationToken)
    {
        var applicant = await dbContext.Applicants.AsNoTracking()
            .Where(a => a.Id == applicantId)
            .Select(a => new { a.FullName, a.Cnic, a.MembershipNumber })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Applicant", applicantId);

        var fundSummaries = await QueryLedgerRowsAsync(applicantId, cancellationToken);

        var entries = await dbContext.Database.SqlQuery<ApplicantLedgerEntryDto>(
            $"""
            SELECT p.payment_date AS date, 'Disbursement' AS entry_type, p.payment_number AS reference_number,
                   p.fund_category_id, fc.name AS fund_category_name, app.application_number, p.amount, p.status
            FROM payments p
            JOIN applications app ON app.id = p.application_id
            JOIN fund_categories fc ON fc.id = p.fund_category_id
            WHERE app.applicant_id = {applicantId}

            UNION ALL

            SELECT r.repayment_date AS date, 'Repayment' AS entry_type, r.repayment_number AS reference_number,
                   la.fund_category_id, fc2.name AS fund_category_name, app2.application_number, r.amount, r.status
            FROM loan_repayments r
            JOIN loan_agreements la ON la.id = r.loan_agreement_id
            JOIN applications app2 ON app2.id = la.application_id
            JOIN fund_categories fc2 ON fc2.id = la.fund_category_id
            WHERE app2.applicant_id = {applicantId}

            ORDER BY date, reference_number
            """).ToListAsync(cancellationToken);

        return new ApplicantLedgerDto(applicantId, applicant.FullName, applicant.Cnic, applicant.MembershipNumber, fundSummaries, entries);
    }

    /// <summary>
    /// Row query for one applicant across every fund they've touched. "pairs" is D6's population
    /// rule: any applicant with an application, payment, or loan agreement against a fund (any
    /// status) counts, checked three ways because a payment/loan agreement's own
    /// <c>fund_category_id</c> can in principle diverge from its application's current one.
    /// <c>OutstandingRecoverable</c> includes <c>Cancelled</c> agreements alongside <c>Active</c>
    /// ones per the verified D2 finding: <c>LoanAgreement.EnsureCancellable</c> only blocks
    /// cancellation once something has been repaid, not once principal has been disbursed, so a
    /// Cancelled agreement can still carry disbursed-but-unrepaid principal.
    /// </summary>
    private async Task<List<FundApplicantLedgerRowDto>> QueryLedgerRowsAsync(Guid applicantId, CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQuery<FundApplicantLedgerRowDto>(
            $"""
            WITH pairs AS (
                SELECT DISTINCT ap.applicant_id, ap.fund_category_id
                FROM applications ap
                WHERE ap.applicant_id = {applicantId}
                UNION
                SELECT DISTINCT app.applicant_id, p.fund_category_id
                FROM payments p
                JOIN applications app ON app.id = p.application_id
                WHERE app.applicant_id = {applicantId}
                UNION
                SELECT DISTINCT app.applicant_id, la.fund_category_id
                FROM loan_agreements la
                JOIN applications app ON app.id = la.application_id
                WHERE app.applicant_id = {applicantId}
            ),
            ledger AS (
                SELECT
                    a.id AS applicant_id, a.full_name, a.cnic, a.membership_number,
                    fc.id AS fund_category_id, fc.name AS fund_category_name,
                    COALESCE(pay.total, 0)::numeric(18,2) AS total_received,
                    COALESCE(rep.total, 0)::numeric(18,2) AS total_repaid,
                    COALESCE(rec.outstanding, 0)::numeric(18,2) AS outstanding_recoverable,
                    COALESCE(wo.total, 0)::numeric(18,2) AS written_off_amount,
                    CASE
                        WHEN COALESCE(agr.agreement_count, 0) = 0 THEN 'NotApplicable'
                        WHEN COALESCE(rec.outstanding, 0) > 0 THEN 'Outstanding'
                        WHEN COALESCE(wo.total, 0) > 0 THEN 'WrittenOff'
                        ELSE 'Cleared'
                    END AS recovery_status,
                    (COALESCE(pay.cnt, 0) + COALESCE(rep.cnt, 0))::int AS activity_count,
                    GREATEST(pay.last_date, rep.last_date) AS last_activity_date
                FROM pairs pr
                JOIN applicants a ON a.id = pr.applicant_id AND a.is_deleted = false
                JOIN fund_categories fc ON fc.id = pr.fund_category_id
                LEFT JOIN LATERAL (
                    SELECT SUM(p.amount) AS total, COUNT(*) AS cnt, MAX(p.payment_date) AS last_date
                    FROM payments p
                    JOIN applications app ON app.id = p.application_id
                    WHERE app.applicant_id = pr.applicant_id AND p.fund_category_id = pr.fund_category_id AND p.status = 'Completed'
                ) pay ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(r.amount) AS total, COUNT(*) AS cnt, MAX(r.repayment_date) AS last_date
                    FROM loan_repayments r
                    JOIN loan_agreements la2 ON la2.id = r.loan_agreement_id
                    JOIN applications app2 ON app2.id = la2.application_id
                    WHERE app2.applicant_id = pr.applicant_id AND la2.fund_category_id = pr.fund_category_id AND r.status = 'Completed'
                ) rep ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(vlb.outstanding_balance) AS outstanding
                    FROM loan_agreements la3
                    JOIN applications app3 ON app3.id = la3.application_id
                    JOIN vw_loan_balances vlb ON vlb.loan_agreement_id = la3.id
                    WHERE app3.applicant_id = pr.applicant_id AND la3.fund_category_id = pr.fund_category_id
                      AND la3.status IN ('Active', 'Cancelled')
                ) rec ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(vlb.outstanding_balance) AS total
                    FROM loan_agreements la4
                    JOIN applications app4 ON app4.id = la4.application_id
                    JOIN vw_loan_balances vlb ON vlb.loan_agreement_id = la4.id
                    WHERE app4.applicant_id = pr.applicant_id AND la4.fund_category_id = pr.fund_category_id
                      AND la4.status = 'WrittenOff'
                ) wo ON true
                LEFT JOIN LATERAL (
                    SELECT COUNT(*) AS agreement_count
                    FROM loan_agreements la5
                    JOIN applications app5 ON app5.id = la5.application_id
                    WHERE app5.applicant_id = pr.applicant_id AND la5.fund_category_id = pr.fund_category_id
                ) agr ON true
            )
            SELECT * FROM ledger
            ORDER BY full_name, fund_category_name
            """).ToListAsync(cancellationToken);
}
