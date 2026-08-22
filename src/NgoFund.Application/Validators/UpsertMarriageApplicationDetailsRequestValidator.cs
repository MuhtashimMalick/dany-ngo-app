using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Applications;

namespace NgoFund.Application.Validators;

public class UpsertMarriageApplicationDetailsRequestValidator : AbstractValidator<UpsertMarriageApplicationDetailsRequest>
{
    public UpsertMarriageApplicationDetailsRequestValidator()
    {
        RuleFor(x => x.BrideName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GroomName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BrideCnic).MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage)
            .When(x => !string.IsNullOrEmpty(x.BrideCnic));
        RuleFor(x => x.GroomMobile).MaximumLength(12).Matches(PakistaniFormats.MobilePhonePattern)
            .WithMessage(PakistaniFormats.MobilePhoneMessage)
            .When(x => !string.IsNullOrEmpty(x.GroomMobile));

        RuleFor(x => x.BrideMaritalStatus)
            .Must(v => TryMap(v)).WithMessage("BrideMaritalStatus is not a recognized marital status.")
            .When(x => x.BrideMaritalStatus is not null);

        RuleFor(x => x.GroomMaritalStatus)
            .Must(v => TryMap(v)).WithMessage("GroomMaritalStatus is not a recognized marital status.")
            .When(x => x.GroomMaritalStatus is not null);

        // Mirrors the DB CHECK (ck_marriage_application_details_rukhsati_after_nikah) so staff get
        // a friendly validation error instead of a raw constraint-violation SQL error.
        RuleFor(x => x)
            .Must(x => x.RukhsatiDate is null || x.NikahDate is null || x.RukhsatiDate >= x.NikahDate)
            .WithMessage("RukhsatiDate cannot be before NikahDate.")
            .WithName("RukhsatiDate");
    }

    private static bool TryMap(string? value)
    {
        try
        {
            MaritalStatusMapper.MapFormLabel(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
