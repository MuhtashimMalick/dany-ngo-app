using NgoFund.Contracts.FundCategories;

namespace NgoFund.Contracts.Reports;

public record DashboardSummaryDto(
    IReadOnlyList<FundBalanceDto> FundBalances,
    IReadOnlyList<StatusBreakdownDto> ApplicationsByStatus,
    IReadOnlyList<CategoryBreakdownDto> ApplicationsByCategory,
    IReadOnlyList<CategoryBreakdownDto> PendingApplicationsByCategory,
    decimal DonationsThisMonth,
    decimal PaymentsThisMonth);
