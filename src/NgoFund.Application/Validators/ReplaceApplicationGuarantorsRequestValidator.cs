using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Validators;

public class ReplaceApplicationGuarantorsRequestValidator : AbstractValidator<ReplaceApplicationGuarantorsRequest>
{
    public ReplaceApplicationGuarantorsRequestValidator()
    {
        RuleForEach(x => x.Guarantors).ChildRules(g =>
        {
            g.RuleFor(e => e.SequenceNumber).GreaterThan(0);
            g.RuleFor(e => e.FullName).NotEmpty().MaximumLength(200);

            // A guarantor row only counts toward ApplicationCategory.RequiresGuarantors (the
            // Approved-transition guarantor gate) once it has both — a name alone isn't enough to
            // actually chase the guarantor down if the applicant defaults. Blocks the save itself,
            // distinct from the ROZGAR.GUARANTOR_CNIC/GUARANTOR_MEMBERSHIP_CARD document-upload
            // slots, which are a separate (documents, not data fields) completeness concern.
            g.RuleFor(e => e.Cnic).NotEmpty().MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
                .WithMessage(PakistaniFormats.CnicMessage);
            g.RuleFor(e => e.MembershipNumber).NotEmpty().MaximumLength(PakistaniFormats.JamaatMembershipMaxLength);

            // v1.6 (feedback round 3, item 7): a guarantor must be reachable — name/CNIC alone
            // isn't enough to actually chase them down if the applicant defaults.
            g.RuleFor(e => e.PhoneMobile).NotEmpty().MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
                .WithMessage(PakistaniFormats.MobilePhoneMessage);
            g.RuleFor(e => e.PhoneHome).NotEmpty().MaximumLength(30);
            g.RuleFor(e => e.PhoneOffice).NotEmpty().MaximumLength(30);
            g.RuleFor(e => e.ResidentialAddress).NotEmpty();
            g.RuleFor(e => e.BusinessAddress).NotEmpty();
        });

        RuleFor(x => x.Guarantors)
            .Must(list => list.Select(g => g.SequenceNumber).Distinct().Count() == list.Count)
            .WithMessage("SequenceNumber must be unique within one application's guarantor list.");
    }
}
