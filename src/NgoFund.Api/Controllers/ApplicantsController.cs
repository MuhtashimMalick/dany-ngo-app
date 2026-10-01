using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Ledgers;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/applicants")]
[Authorize]
public class ApplicantsController(
    IApplicantService applicantService,
    IApplicantLedgerService applicantLedgerService,
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

    // Up to 4 inline base64 uploads (CnicFront/CnicBack/MembershipCard/Photo) at 10 MB decoded
    // each: base64 inflates size by ~4/3, so 4 * 10 MB * 4/3 ~= 53 MB of raw JSON, plus headroom
    // for the rest of the request body — comfortably past Kestrel's 30 MB default.
    [HttpPost]
    [HasPermission("applicants.create")]
    [RequestSizeLimit(64 * 1024 * 1024)]
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

    /// <summary>"Set" semantics: replaces whatever document this applicant has on file for
    /// <paramref name="documentType"/> (CnicFront/CnicBack/MembershipCard only — not
    /// ApplicantPhoto, which has its own separate <c>PUT /{id}/photo</c> flow), in place.
    /// See <see cref="IApplicantService.ReplaceProfileDocumentAsync"/>.</summary>
    [HttpPut("{id:guid}/documents/{documentType}")]
    [HasPermission("applicants.edit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> ReplaceProfileDocument(Guid id, string documentType, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await applicantService.ReplaceProfileDocumentAsync(id, documentType, stream, file.FileName, file.ContentType, ct);
        return Ok(result);
    }

    /// <summary>This applicant's complete financial history across every fund. Not paged — one person's history is bounded. Gated on both money-viewing permissions, not a new one.</summary>
    [HttpGet("{id:guid}/ledger")]
    [HasPermission("payments.view")]
    [HasPermission("loans.view")]
    public async Task<ActionResult<ApplicantLedgerDto>> GetLedger(Guid id, CancellationToken cancellationToken)
        => Ok(await applicantLedgerService.GetApplicantLedgerAsync(id, cancellationToken));
}
