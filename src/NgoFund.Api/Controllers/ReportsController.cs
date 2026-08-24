using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Reports;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("dashboard")]
    [HasPermission("dashboard.view")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await reportService.GetDashboardSummaryAsync(cancellationToken));

    [HttpGet("dashboard/insights")]
    [HasPermission("dashboard.view")]
    public async Task<ActionResult<DashboardInsightsDto>> GetDashboardInsights(CancellationToken cancellationToken)
        => Ok(await reportService.GetDashboardInsightsAsync(cancellationToken));

    [HttpGet("donations/monthly")]
    [HasPermission("reports.view")]
    public async Task<ActionResult<IReadOnlyList<MonthlySummaryRowDto>>> GetMonthlyDonations(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => Ok(await reportService.GetMonthlyDonationsAsync(from, to, cancellationToken));

    [HttpGet("payments/monthly")]
    [HasPermission("reports.view")]
    public async Task<ActionResult<IReadOnlyList<MonthlySummaryRowDto>>> GetMonthlyPayments(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => Ok(await reportService.GetMonthlyPaymentsAsync(from, to, cancellationToken));

    [HttpGet("loan-repayments")]
    [HasPermission("reports.view")]
    public async Task<ActionResult<IReadOnlyList<MonthlySummaryRowDto>>> GetLoanRepayments(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => Ok(await reportService.GetMonthlyLoanRepaymentsAsync(from, to, cancellationToken));
}
