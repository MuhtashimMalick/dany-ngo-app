using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.FundCategories;
using NgoFund.Contracts.Ledgers;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/fund-categories")]
[Authorize]
public class FundCategoriesController(
    IFundCategoryService fundCategoryService,
    IFundTransactionLedgerService fundTransactionLedgerService,
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
