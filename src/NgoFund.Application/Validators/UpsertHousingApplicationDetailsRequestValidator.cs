using FluentValidation;
using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Validators;

public class UpsertHousingApplicationDetailsRequestValidator : AbstractValidator<UpsertHousingApplicationDetailsRequest>
{
    public UpsertHousingApplicationDetailsRequestValidator()
    {
        RuleFor(x => x.ApplicantAge).GreaterThan(0).When(x => x.ApplicantAge is not null);
        RuleFor(x => x.CurrentHouseValue).GreaterThanOrEqualTo(0).When(x => x.CurrentHouseValue is not null);
        RuleFor(x => x.MonthlyRent).GreaterThanOrEqualTo(0).When(x => x.MonthlyRent is not null);
        RuleFor(x => x.AdvancePaid).GreaterThanOrEqualTo(0).When(x => x.AdvancePaid is not null);
        RuleFor(x => x.YearsAtCurrentAddress).GreaterThanOrEqualTo(0).When(x => x.YearsAtCurrentAddress is not null);
        RuleFor(x => x.PreviousAssistanceDetails).MaximumLength(2000);
    }
}
