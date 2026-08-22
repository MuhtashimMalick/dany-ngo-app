using FluentValidation;
using NgoFund.Contracts.Documents;

namespace NgoFund.Application.Validators;

/// <summary>Same content-type allowlist as <c>DocumentService</c>'s multipart upload path — kept
/// in sync manually since Application doesn't depend on Infrastructure; both funnel through
/// <c>DocumentService.UploadAsync</c>, which re-checks (and content-sniffs) regardless.</summary>
public class InlineFileUploadValidator : AbstractValidator<InlineFileUpload>
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    public InlineFileUploadValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);

        RuleFor(x => x.ContentType).NotEmpty()
            .Must(t => AllowedContentTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ContentType must be one of: {string.Join(", ", AllowedContentTypes)}.");

        RuleFor(x => x.ContentBase64).NotEmpty()
            .Must(BeValidBase64)
            .WithMessage("ContentBase64 must be valid, non-empty base64 content no larger than 10 MB decoded.");
    }

    private static bool BeValidBase64(string base64)
    {
        try
        {
            var length = Convert.FromBase64String(base64).Length;
            return length > 0 && length <= InlineFileUpload.MaxSizeBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
