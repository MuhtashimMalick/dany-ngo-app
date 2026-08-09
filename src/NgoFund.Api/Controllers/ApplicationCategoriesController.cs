using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.ApplicationCategories;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/application-categories")]
[Authorize]
public class ApplicationCategoriesController(
    IApplicationCategoryService applicationCategoryService,
    IValidator<CreateApplicationCategoryRequest> createValidator,
    IValidator<UpdateApplicationCategoryRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("applicationcategories.view")]
    public async Task<ActionResult<IReadOnlyList<ApplicationCategoryDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await applicationCategoryService.GetAllAsync(cancellationToken));

    [HttpPost]
    [HasPermission("applicationcategories.manage")]
    public async Task<ActionResult<ApplicationCategoryDto>> Create(CreateApplicationCategoryRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await applicationCategoryService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("applicationcategories.manage")]
    public async Task<ActionResult<ApplicationCategoryDto>> Update(Guid id, UpdateApplicationCategoryRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await applicationCategoryService.UpdateAsync(id, request, cancellationToken));
    }
}
