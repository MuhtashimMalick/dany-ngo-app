using FluentValidation;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreateApplicantRequestValidator : AbstractValidator<CreateApplicantRequest>
{
    public CreateApplicantRequestValidator()
    {
        var inlineFileUploadValidator = new InlineFileUploadValidator();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnic).NotEmpty().MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage);
        RuleFor(x => x.Gender).NotEmpty().Must(v => Enum.TryParse<Gender>(v, out _))
            .WithMessage($"Gender must be one of: {string.Join(", ", Enum.GetNames<Gender>())}.");
        RuleFor(x => x.MaritalStatus).Must(v => v is null || Enum.TryParse<MaritalStatus>(v, out _))
            .WithMessage($"MaritalStatus must be one of: {string.Join(", ", Enum.GetNames<MaritalStatus>())}.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.MembershipNumber).MaximumLength(PakistaniFormats.JamaatMembershipMaxLength);
        RuleFor(x => x.FatherMembershipNumber).MaximumLength(PakistaniFormats.JamaatMembershipMaxLength);
        RuleFor(x => x.Phone).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.AlternatePhone).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.AlternatePhone));
        RuleFor(x => x.WhatsappNumber).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.WhatsappNumber));

        // A3/v1.4: CNIC front, CNIC back, and the Jamaat membership card must be uploaded at
        // applicant creation — the profile photo stays optional. Editing an existing applicant
        // (UpdateApplicantRequest) is deliberately not gated by this rule.
        RuleFor(x => x.CnicFront).NotNull().WithMessage("A scan of the applicant's CNIC is required.");
        RuleFor(x => x.CnicBack).NotNull().WithMessage("A scan of the back of the applicant's CNIC is required.");
        RuleFor(x => x.MembershipCard).NotNull().WithMessage("A scan of the applicant's Jamaat membership card is required.");
        RuleFor(x => x.CnicFront!).SetValidator(inlineFileUploadValidator).When(x => x.CnicFront is not null);
        RuleFor(x => x.CnicBack!).SetValidator(inlineFileUploadValidator).When(x => x.CnicBack is not null);
        RuleFor(x => x.MembershipCard!).SetValidator(inlineFileUploadValidator).When(x => x.MembershipCard is not null);
        RuleFor(x => x.Photo!).SetValidator(inlineFileUploadValidator).When(x => x.Photo is not null);
    }
}
