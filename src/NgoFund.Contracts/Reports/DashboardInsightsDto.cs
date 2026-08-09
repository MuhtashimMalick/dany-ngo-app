namespace NgoFund.Contracts.Reports;

/// <summary>
/// Extended dashboard KPIs/visualizations, separate from <see cref="DashboardSummaryDto"/> so the
/// existing summary endpoint/contract is untouched. All windowed fields cover the trailing 12
/// months from today; ratios/percentages are computed client-side from these raw counts.
/// </summary>
public record DashboardInsightsDto(
    IReadOnlyList<StatusBreakdownDto> ApplicationsByStatusLast12Months,
    ProcessingTimeDto ProcessingTime,
    IReadOnlyList<FundCommitmentDto> OutstandingCommitments,
    DonorMetricsDto Donors,
    int DonationCountThisMonth,
    decimal LargestDonationThisMonth,
    IReadOnlyList<LabeledAmountDto> DonationsByMethodLast12Months,
    IReadOnlyList<LabeledAmountDto> PaymentsByMethodLast12Months,
    IReadOnlyList<LabeledAmountDto> OpenApplicationsByPriority);
