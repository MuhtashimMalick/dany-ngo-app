using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Ledgers;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/fund-categories")]
[Authorize]
public partial class FundCategoriesController(
    IFundCategoryService fundCategoryService,
    IFundTransactionLedgerService fundTransactionLedgerService,
    IFundLedgerCsvBuilder fundLedgerCsvBuilder,
    IFundLedgerPdfBuilder fundLedgerPdfBuilder,
    IValidator<CreateFundCategoryRequest> createValidator,
    IValidator<UpdateFundCategoryRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("fundcategories.view")]
    public async Task<ActionResult<IReadOnlyList<FundCategoryDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await fundCategoryService.GetAllAsync(cancellationToken));

    [HttpGet("balances")]
    [HasPermission("fundcategories.view")]
    public async Task<ActionResult<IReadOnlyList<FundBalanceDto>>> GetBalances(CancellationToken cancellationToken)
        => Ok(await fundCategoryService.GetBalancesAsync(cancellationToken));

    /// <summary>The chronological, running-balance ledger for this fund (client feedback: Date /
    /// Category / No. / Name / GRN / OG / Total). Exposes donation and payment data, gated on both
    /// money-viewing permissions, not a new one.</summary>
    [HttpGet("{id:guid}/transaction-ledger")]
    [HasPermission("donations.view")]
    [HasPermission("payments.view")]
    public async Task<ActionResult<PagedResult<FundTransactionLedgerRowDto>>> GetTransactionLedger(
        Guid id, [FromQuery] PagedQuery query, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
        => Ok(await fundTransactionLedgerService.GetFundTransactionLedgerAsync(id, query, fromDate, toDate, cancellationToken));

    /// <summary>CSV/PDF export of the same ledger as <see cref="GetTransactionLedger"/>, unpaginated.
    /// Gated on the ledger's own view permissions plus <c>reports.export</c> — a user who can only
    /// view the ledger on screen cannot download it.</summary>
    [HttpGet("{id:guid}/transaction-ledger/export")]
    [HasPermission("donations.view")]
    [HasPermission("payments.view")]
    [HasPermission("reports.export")]
    public async Task<IActionResult> ExportTransactionLedger(
        Guid id, [FromQuery] FundLedgerExportFormat format, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var funds = await fundCategoryService.GetAllAsync(cancellationToken);
        var fund = funds.SingleOrDefault(f => f.Id == id) ?? throw new EntityNotFoundException("FundCategory", id);

        var rows = await fundTransactionLedgerService.GetFullFundTransactionLedgerAsync(id, fromDate, toDate, search, cancellationToken);

        // Rows are already ordered by transaction date — derive the range actually covered when
        // the caller didn't pin one, falling back to today for an empty result set.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveFrom = fromDate ?? (rows.Count > 0 ? rows[0].TransactionDate : today);
        var effectiveTo = toDate ?? (rows.Count > 0 ? rows[^1].TransactionDate : today);
        var fileNameStem = $"{effectiveFrom:yyyy-MM-dd}_to_{effectiveTo:yyyy-MM-dd}_{Slugify(fund.Name)}_report";

        if (format == FundLedgerExportFormat.Csv)
        {
            return File(fundLedgerCsvBuilder.Build(rows), "text/csv", $"{fileNameStem}.csv");
        }

        return File(fundLedgerPdfBuilder.Build(fund.Name, effectiveFrom, effectiveTo, rows), "application/pdf", $"{fileNameStem}.pdf");
    }

    // "Zakat Fund" -> "zakat", "General Fund" -> "general" for the two currently-seeded funds
    // (see FundCategorySeed) — strips the generic word "Fund" rather than hardcoding an
    // IsZakat ? "zakat" : "general" branch, so a third fund slugifies sensibly too.
    private static string Slugify(string name)
    {
        var withoutFundWord = FundWordRegex().Replace(name, "");
        var slug = NonAlphanumericRegex().Replace(withoutFundWord.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "fund" : slug;
    }

    [GeneratedRegex(@"\bfund\b", RegexOptions.IgnoreCase)]
    private static partial Regex FundWordRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [HttpPost]
    [HasPermission("fundcategories.manage")]
    public async Task<ActionResult<FundCategoryDto>> Create(CreateFundCategoryRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await fundCategoryService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("fundcategories.manage")]
    public async Task<ActionResult<FundCategoryDto>> Update(Guid id, UpdateFundCategoryRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await fundCategoryService.UpdateAsync(id, request, cancellationToken));
    }
}
