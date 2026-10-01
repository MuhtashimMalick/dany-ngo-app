using FluentValidation;
using NgoFund.Contracts.Intake;

namespace NgoFund.Application.Validators;

/// <summary>Request-shape guardrails for the intake endpoint (C7) — deliberately loose on
/// individual answer content (the mapper is the lenient, tolerant layer); this only bounds sizes
/// so one runaway/malicious payload can't blow up memory or the DB.</summary>
public class GoogleFormSubmissionRequestValidator : AbstractValidator<GoogleFormSubmissionRequest>
{
    public GoogleFormSubmissionRequestValidator()
    {
        RuleFor(x => x.FormResponseId).NotEmpty().Length(1, 100).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.ApplicationType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RespondentEmail).MaximumLength(200);
        RuleFor(x => x.Answers).Must(a => a.Count <= 300).WithMessage("A submission may not carry more than 300 answers.");
        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(e => e.Title).NotEmpty().MaximumLength(300);
            a.RuleFor(e => e.Values).Must(v => v.Count <= 50 && v.All(x => x.Length <= 5000))
                .WithMessage("Each answer may carry at most 50 values of at most 5000 characters.");
        });
        RuleFor(x => x.Files).Must(f => f.Count <= 50).WithMessage("A submission may not carry more than 50 file-manifest entries.");
        RuleForEach(x => x.Files).ChildRules(f =>
        {
            f.RuleFor(e => e.DriveFileId).NotEmpty().MaximumLength(200);
            f.RuleFor(e => e.FileName).NotEmpty().MaximumLength(260);
            f.RuleFor(e => e.MimeType).NotEmpty().MaximumLength(150);
        });
    }
}
