using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NgoFund.Api.Authentication;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Intake;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Api.Controllers;

/// <summary>
/// C7: the ONLY controller in the system that accepts the "GoogleFormIntake" authentication
/// scheme (an API key, not a JWT) — every route here is under <c>/api/intake/google-form</c>,
/// which the port-gate middleware (see <c>Program.cs</c>) is the only thing ngrok's public tunnel
/// can reach on the intake port. Never stacks <c>[HasPermission]</c>: the intake key itself is the
/// authorization.
/// </summary>
[ApiController]
[Route("api/intake/google-form")]
[Authorize(AuthenticationSchemes = GoogleFormIntakeAuthenticationHandler.SchemeName)]
[EnableRateLimiting("intake")]
public class IntakeController(IGoogleFormIntakeService intakeService, IValidator<GoogleFormSubmissionRequest> submissionValidator) : ControllerBase
{
    [HttpPost("submissions")]
    [RequestSizeLimit(1 * 1024 * 1024)]
    public async Task<ActionResult<GoogleFormSubmissionResponse>> Submit(GoogleFormSubmissionRequest request, CancellationToken cancellationToken)
    {
        await submissionValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await intakeService.SubmitAsync(request, cancellationToken);
        return StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result);
    }

    [HttpPost("submissions/{formResponseId}/documents")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> UploadDocument(
        string formResponseId, IFormFile file, [FromForm] string questionTitle, [FromForm] string? questionHelpText,
        [FromForm] string driveFileId, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var (document, created) = await intakeService.UploadDocumentAsync(
                formResponseId, questionTitle, questionHelpText, driveFileId, file.FileName, file.ContentType, stream, cancellationToken);
            return StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, document);
        }
        catch (InvalidFileException ex)
        {
            // The intake brief calls for 415 specifically here — every other caller of
            // IDocumentService.UploadAsync gets DomainExceptionHandler's default 422, but Apps
            // Script's retry logic treats 415 as non-retryable, which is the correct behavior for
            // "this file type will never be accepted."
            return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported file type.", detail: ex.Message);
        }
    }

    /// <summary>For Apps Script's <c>setup()</c> to verify connectivity and the configured key.</summary>
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok" });
}
