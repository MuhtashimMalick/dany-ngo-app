using NgoFund.Contracts.Reports;

namespace NgoFund.Application.Abstractions;

public interface IReportService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken);

    Task<DashboardInsightsDto> GetDashboardInsightsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyDonationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlySummaryRowDto>> GetMonthlyPaymentsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
