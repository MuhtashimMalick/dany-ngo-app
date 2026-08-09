using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/applicants")]
[Authorize]
public class ApplicantsController(
    IApplicantService applicantService,
    IValidator<CreateApplicantRequest> createValidator,
    IValidator<UpdateApplicantRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("applicants.view")]
    public async Task<ActionResult<PagedResult<ApplicantDto>>> GetApplicants([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => Ok(await applicantService.GetApplicantsAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("applicants.view")]
    public async Task<ActionResult<ApplicantDto>> GetApplicant(Guid id, CancellationToken cancellationToken)
        => Ok(await applicantService.GetApplicantByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission("applicants.create")]
    public async Task<ActionResult<ApplicantDto>> Create(CreateApplicantRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await applicantService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetApplicant), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("applicants.edit")]
    public async Task<ActionResult<ApplicantDto>> Update(Guid id, UpdateApplicantRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await applicantService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>"Delete" per the scope document — implemented as a soft delete, see IApplicantService.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("applicants.delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await applicantService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/photo")]
    [HasPermission("applicants.edit")]
    public async Task<IActionResult> SetProfilePhoto(Guid id, [FromQuery] Guid documentId, CancellationToken cancellationToken)
    {
        await applicantService.SetProfilePhotoAsync(id, documentId, cancellationToken);
        return NoContent();
    }
}
