using FluentValidation;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class UpdateApplicantRequestValidator : AbstractValidator<UpdateApplicantRequest>
{
    public UpdateApplicantRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnic).NotEmpty().MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage);
        // A5: unlike CreateApplicantRequestValidator, Gender is optional here — a Google-Form-
        // created applicant may still have no gender on file, and the blacklist/edit flow must
        // keep working for them. When present it must still be a recognized value.
        RuleFor(x => x.Gender).Must(v => Enum.TryParse<Gender>(v, out _))
            .When(x => x.Gender is not null)
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
        RuleFor(x => x.BlacklistReason).MaximumLength(500);
    }
}
