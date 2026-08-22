using NgoFund.Contracts.Reports;

namespace NgoFund.Application.Abstractions;

public interface IReportService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken);

    Task<DashboardInsightsDto> GetDashboardInsightsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyDonationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Completed loan repayments only, grouped by month and the agreement's fund — reuses <see cref="MonthlySummaryRowDto"/> since the shape is identical to donations/payments.</summary>
    Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyLoanRepaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
