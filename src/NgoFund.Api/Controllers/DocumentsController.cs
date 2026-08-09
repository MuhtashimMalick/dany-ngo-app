using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Api.Authorization;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;

namespace NgoFund.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpPost]
    [HasPermission("documents.upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload(
        IFormFile file,
        [FromQuery] string documentType,
        [FromQuery] Guid? applicantId,
        [FromQuery] Guid? applicationId,
        [FromQuery] Guid? donationId,
        [FromQuery] Guid? paymentId,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await documentService.UploadAsync(
            stream, file.FileName, file.ContentType, documentType, applicantId, applicationId, donationId, paymentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("documents.view")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (content, metadata) = await documentService.DownloadAsync(id, cancellationToken);
        return File(content, metadata.ContentType, metadata.FileName);
    }

    [HttpGet("by-applicant/{applicantId:guid}")]
    [HasPermission("documents.view")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetForApplicant(Guid applicantId, CancellationToken cancellationToken)
        => Ok(await documentService.GetForApplicantAsync(applicantId, cancellationToken));

    [HttpGet("by-application/{applicationId:guid}")]
    [HasPermission("documents.view")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetForApplication(Guid applicationId, CancellationToken cancellationToken)
        => Ok(await documentService.GetForApplicationAsync(applicationId, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission("documents.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await documentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
