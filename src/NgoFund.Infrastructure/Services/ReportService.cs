using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Application.Reports;
using NgoFund.Contracts.Reports;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Read-only aggregation queries for the dashboard and monthly reports. Deliberately has no
/// mutating methods and depends on <see cref="IFundCategoryService"/> for fund balances rather
/// than re-querying <c>vw_fund_balances</c> itself — one source of truth for "what is a fund's
/// balance", reused here instead of duplicated.
/// </summary>
public class ReportService(AppDbContext dbContext, IFundCategoryService fundCategoryService) : IReportService
{
    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var fundBalances = await fundCategoryService.GetBalancesAsync(cancellationToken);

        var statusGroups = await dbContext.Applications
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new StatusBreakdownDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        // Grouped by the scalar FK (not the ApplicationCategory.Name navigation) and the nullable
        // Sum left un-coalesced: EF/Npgsql can't translate a Join-based GroupBy combined with
        // Sum(x ?? default) as one query. Names are resolved and the coalesce applied client-side
        // below instead, against a tiny (≤ a few dozen row) category table.
        var categoryNames = await dbContext.ApplicationCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var categoryRows = await dbContext.Applications
            .AsNoTracking()
            .GroupBy(a => a.ApplicationCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count(), RequestedTotal = g.Sum(a => a.RequestedAmount), ApprovedTotal = g.Sum(a => a.ApprovedAmount) })
            .ToListAsync(cancellationToken);

        var categoryGroups = categoryRows
            .Select(r => new CategoryBreakdownDto(categoryNames.GetValueOrDefault(r.CategoryId, "Unknown"), r.Count, r.RequestedTotal, r.ApprovedTotal ?? 0m))
            .OrderByDescending(c => c.Count)
            .ToList();

        var pendingCategoryRows = await dbContext.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.UnderReview || a.Status == ApplicationStatus.OnHold)
            .GroupBy(a => a.ApplicationCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count(), RequestedTotal = g.Sum(a => a.RequestedAmount), ApprovedTotal = g.Sum(a => a.ApprovedAmount) })
            .ToListAsync(cancellationToken);

        var pendingCategoryGroups = pendingCategoryRows
            .Select(r => new CategoryBreakdownDto(categoryNames.GetValueOrDefault(r.CategoryId, "Unknown"), r.Count, r.RequestedTotal, r.ApprovedTotal ?? 0m))
            .OrderByDescending(c => c.Count)
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var donationsThisMonth = await dbContext.Donations
            .AsNoTracking()
            .Where(d => d.Status == DonationStatus.Confirmed && d.DonationDate >= monthStart)
            .SumAsync(d => (decimal?)d.Amount, cancellationToken) ?? 0m;

        var paymentsThisMonth = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate >= monthStart)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return new DashboardSummaryDto(fundBalances, statusGroups, categoryGroups, pendingCategoryGroups, donationsThisMonth, paymentsThisMonth);
    }

    /// <summary>
    /// Extended KPIs/visualizations for the dashboard (separate endpoint/DTO from
    /// <see cref="GetDashboardSummaryAsync"/> so that contract stays untouched). Runs its 9
    /// queries sequentially — <see cref="AppDbContext"/> is not thread-safe, so no
    /// <c>Task.WhenAll</c> here.
    ///
    /// Query 2 (processing time) materializes one row per Approved/Rejected decision within the
    /// trailing 12 months and computes mean/median client-side, because TimeSpan/DateOnly
    /// arithmetic inside an EF aggregate doesn't reliably translate on Npgsql. Acceptable at
    /// Phase-1 volume; note that <c>application_status_history.changed_at</c> has no index today
    /// (only <c>application_id</c> is indexed) — worth revisiting if this becomes slow, but not a
    /// migration to add in this pass.
    /// </summary>
    public async Task<DashboardInsightsDto> GetDashboardInsightsAsync(CancellationToken cancellationToken)
    {
        var fundBalances = await fundCategoryService.GetBalancesAsync(cancellationToken);
        var fundNames = fundBalances.ToDictionary(f => f.FundCategoryId, f => f.Name);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddMonths(-12);
        var windowStartUtc = new DateTimeOffset(windowStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowDays = today.DayNumber - windowStart.DayNumber;

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = new DateTimeOffset(monthStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var lastMonthStart = monthStart.AddMonths(-1);
        var lastMonthStartUtc = new DateTimeOffset(lastMonthStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // 1. Application status counts, windowed to the last 12 months by application_date.
        var statusGroups = await dbContext.Applications
            .AsNoTracking()
            .Where(a => a.ApplicationDate >= windowStart)
            .GroupBy(a => a.Status)
            .Select(g => new StatusBreakdownDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        // 2. Raw decision rows for processing-time calculation (see method XML doc above).
        var decisionRows = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(h => (h.ToStatus == ApplicationStatus.Approved || h.ToStatus == ApplicationStatus.Rejected) && h.ChangedAt >= windowStartUtc)
            .Select(h => new { h.ApplicationId, h.ChangedAt, h.Application.ApplicationDate })
            .ToListAsync(cancellationToken);

        var processingTime = ProcessingTimeCalculator.Calculate(
            decisionRows.Select(r => (r.ApplicationId, r.ApplicationDate, r.ChangedAt)).ToList(),
            windowDays);

        // 3/4. Approved-outstanding per fund = approved+partially-paid applications' approved
        // amount minus completed payments against those same applications, combined client-side
        // and clamped at 0 (a fund can't have negative outstanding commitments).
        var approvedByFund = await dbContext.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.PartiallyPaid)
            .GroupBy(a => a.FundCategoryId)
            .Select(g => new { g.Key, Total = g.Sum(a => a.ApprovedAmount) })
            .ToListAsync(cancellationToken);

        var paidByFund = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && (p.Application.Status == ApplicationStatus.Approved || p.Application.Status == ApplicationStatus.PartiallyPaid))
            .GroupBy(p => p.FundCategoryId)
            .Select(g => new { g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        var paidTotals = paidByFund.ToDictionary(r => r.Key, r => r.Total);
        var outstandingCommitments = approvedByFund
            .Select(r => new FundCommitmentDto(
                r.Key,
                fundNames.GetValueOrDefault(r.Key, "Unknown"),
                Math.Max(0m, (r.Total ?? 0m) - paidTotals.GetValueOrDefault(r.Key, 0m))))
            .OrderByDescending(c => c.ApprovedOutstanding)
            .ToList();

        // 5. Donor metrics as a single GroupBy(_ => 1) aggregate — translates fine on Npgsql
        // (conditional counts become COUNT(*) FILTER (WHERE ...)) and FirstOrDefaultAsync
        // returns null on an empty table instead of throwing, so the empty-database case is
        // handled by the null-coalescing below rather than a separate code path.
        var donorAggregate = await dbContext.Donors
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(d => d.IsActive),
                NewThisMonth = g.Count(d => d.CreatedAt >= monthStartUtc),
                NewLastMonth = g.Count(d => d.CreatedAt >= lastMonthStartUtc && d.CreatedAt < monthStartUtc),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var donorMetrics = new DonorMetricsDto(
            donorAggregate?.Total ?? 0,
            donorAggregate?.Active ?? 0,
            donorAggregate?.NewThisMonth ?? 0,
            donorAggregate?.NewLastMonth ?? 0);

        // 6. This month's donation count/largest amount.
        var donationSizeThisMonth = await dbContext.Donations
            .AsNoTracking()
            .Where(d => d.Status == DonationStatus.Confirmed && d.DonationDate >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Max = g.Max(d => d.Amount) })
            .FirstOrDefaultAsync(cancellationToken);

        // 7. Donations by method, last 12 months.
        var donationsByMethod = await dbContext.Donations
            .AsNoTracking()
            .Where(d => d.Status == DonationStatus.Confirmed && d.DonationDate >= windowStart)
            .GroupBy(d => d.PaymentMethod)
            .Select(g => new LabeledAmountDto(g.Key.ToString(), g.Count(), g.Sum(d => d.Amount)))
            .ToListAsync(cancellationToken);

        // 8. Payments by method, last 12 months.
        var paymentsByMethod = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate >= windowStart)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new LabeledAmountDto(g.Key.ToString(), g.Count(), g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);

        // 9. Open (not yet decided) applications by priority.
        var openByPriority = await dbContext.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.UnderReview || a.Status == ApplicationStatus.OnHold)
            .GroupBy(a => a.Priority)
            .Select(g => new LabeledAmountDto(g.Key.ToString(), g.Count(), g.Sum(a => a.RequestedAmount)))
            .ToListAsync(cancellationToken);

        return new DashboardInsightsDto(
            statusGroups,
            processingTime,
            outstandingCommitments,
            donorMetrics,
            donationSizeThisMonth?.Count ?? 0,
            donationSizeThisMonth?.Max ?? 0m,
            donationsByMethod,
            paymentsByMethod,
            openByPriority);
    }

    public async Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyDonationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Donations
            .AsNoTracking()
            .Where(d => d.Status == DonationStatus.Confirmed && d.DonationDate >= from && d.DonationDate <= to)
            .GroupBy(d => new { d.DonationDate.Year, d.DonationDate.Month, d.FundCategoryId, d.FundCategory.Name })
            .Select(g => new MonthlySummaryRowDto(g.Key.Year, g.Key.Month, g.Key.FundCategoryId, g.Key.Name, g.Sum(d => d.Amount)))
            .ToListAsync(cancellationToken);

        return rows.OrderBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.FundCategoryName).ToList();
    }

    public async Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate >= from && p.PaymentDate <= to)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month, p.FundCategoryId, p.FundCategory.Name })
            .Select(g => new MonthlySummaryRowDto(g.Key.Year, g.Key.Month, g.Key.FundCategoryId, g.Key.Name, g.Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);

        return rows.OrderBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.FundCategoryName).ToList();
    }
}
