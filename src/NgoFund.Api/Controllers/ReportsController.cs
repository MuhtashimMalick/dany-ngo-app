using System.Globalization;
using System.Text;
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

    [HttpGet("donations/monthly/export")]
    [HasPermission("reports.export")]
    public async Task<IActionResult> ExportMonthlyDonations([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await reportService.GetMonthlyDonationsAsync(from, to, cancellationToken);
        return File(ToCsv(rows), "text/csv", $"donations-{from:yyyy-MM}-to-{to:yyyy-MM}.csv");
    }

    [HttpGet("payments/monthly/export")]
    [HasPermission("reports.export")]
    public async Task<IActionResult> ExportMonthlyPayments([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await reportService.GetMonthlyPaymentsAsync(from, to, cancellationToken);
        return File(ToCsv(rows), "text/csv", $"payments-{from:yyyy-MM}-to-{to:yyyy-MM}.csv");
    }

    private static byte[] ToCsv(IReadOnlyList<MonthlySummaryRowDto> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Year,Month,Fund,Total");
        foreach (var row in rows)
        {
            csv.AppendLine($"{row.Year},{row.Month},\"{row.FundCategoryName}\",{row.Total.ToString(CultureInfo.InvariantCulture)}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }
}
