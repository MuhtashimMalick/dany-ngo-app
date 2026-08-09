using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.FundCategories;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/fund-categories")]
[Authorize]
public class FundCategoriesController(
    IFundCategoryService fundCategoryService,
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
