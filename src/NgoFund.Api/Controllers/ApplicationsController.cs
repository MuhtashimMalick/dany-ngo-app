using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController(
    IFundApplicationService applicationService,
    IApplicationDetailsService detailsService,
    IValidator<CreateApplicationRequest> createValidator,
    IValidator<UpdateApplicationRequest> updateValidator,
    IValidator<ChangeApplicationStatusRequest> statusValidator,
    IValidator<AddRemarkRequest> remarkValidator,
    IValidator<UpsertHousingApplicationDetailsRequest> housingValidator,
    IValidator<UpsertMarriageApplicationDetailsRequest> marriageValidator,
    IValidator<UpsertBusinessLoanApplicationDetailsRequest> businessLoanValidator,
    IValidator<ReplaceApplicationGuarantorsRequest> guarantorsValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("applications.view")]
    public async Task<ActionResult<PagedResult<ApplicationDto>>> GetApplications(
        [FromQuery] PagedQuery query, [FromQuery] string? status, [FromQuery] Guid? applicantId,
        [FromQuery] Guid? categoryId, [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        CancellationToken cancellationToken)
        => Ok(await applicationService.GetApplicationsAsync(query, status, applicantId, categoryId, dateFrom, dateTo, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<ApplicationDto>> GetApplication(Guid id, CancellationToken cancellationToken)
        => Ok(await applicationService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("applications.create")]
    public async Task<ActionResult<ApplicationDto>> Create(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await applicationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetApplication), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<ApplicationDto>> Update(Guid id, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await applicationService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/status")]
    [HasPermission("applications.review")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        await applicationService.ChangeStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/completeness")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<ApplicationCompletenessDto>> GetCompleteness(Guid id, CancellationToken cancellationToken)
        => Ok(await applicationService.GetCompletenessAsync(id, cancellationToken));

    [HttpGet("{id:guid}/history")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<IReadOnlyList<ApplicationStatusHistoryDto>>> GetHistory(Guid id, CancellationToken cancellationToken)
        => Ok(await applicationService.GetStatusHistoryAsync(id, cancellationToken));

    [HttpGet("{id:guid}/remarks")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<IReadOnlyList<ApplicationRemarkDto>>> GetRemarks(Guid id, CancellationToken cancellationToken)
        => Ok(await applicationService.GetRemarksAsync(id, cancellationToken));

    [HttpPost("{id:guid}/remarks")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<ApplicationRemarkDto>> AddRemark(Guid id, AddRemarkRequest request, CancellationToken cancellationToken)
    {
        await remarkValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await applicationService.AddRemarkAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/details/housing")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<HousingApplicationDetailsDto>> GetHousingDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await detailsService.GetHousingDetailsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}/details/housing")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<HousingApplicationDetailsDto>> UpsertHousingDetails(Guid id, UpsertHousingApplicationDetailsRequest request, CancellationToken cancellationToken)
    {
        await housingValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await detailsService.UpsertHousingDetailsAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/details/marriage")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<MarriageApplicationDetailsDto>> GetMarriageDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await detailsService.GetMarriageDetailsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}/details/marriage")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<MarriageApplicationDetailsDto>> UpsertMarriageDetails(Guid id, UpsertMarriageApplicationDetailsRequest request, CancellationToken cancellationToken)
    {
        await marriageValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await detailsService.UpsertMarriageDetailsAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/details/business-loan")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<BusinessLoanApplicationDetailsDto>> GetBusinessLoanDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await detailsService.GetBusinessLoanDetailsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}/details/business-loan")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<BusinessLoanApplicationDetailsDto>> UpsertBusinessLoanDetails(Guid id, UpsertBusinessLoanApplicationDetailsRequest request, CancellationToken cancellationToken)
    {
        await businessLoanValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await detailsService.UpsertBusinessLoanDetailsAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/guarantors")]
    [HasPermission("applications.view")]
    public async Task<ActionResult<IReadOnlyList<ApplicationGuarantorDto>>> GetGuarantors(Guid id, CancellationToken cancellationToken)
        => Ok(await detailsService.GetGuarantorsAsync(id, cancellationToken));

    [HttpPut("{id:guid}/guarantors")]
    [HasPermission("applications.edit")]
    public async Task<ActionResult<IReadOnlyList<ApplicationGuarantorDto>>> ReplaceGuarantors(Guid id, ReplaceApplicationGuarantorsRequest request, CancellationToken cancellationToken)
    {
        await guarantorsValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await detailsService.ReplaceGuarantorsAsync(id, request, cancellationToken));
    }
}
