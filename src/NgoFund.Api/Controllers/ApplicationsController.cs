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
    IValidator<CreateApplicationRequest> createValidator,
    IValidator<UpdateApplicationRequest> updateValidator,
    IValidator<ChangeApplicationStatusRequest> statusValidator,
    IValidator<AddRemarkRequest> remarkValidator) : ControllerBase
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
}
