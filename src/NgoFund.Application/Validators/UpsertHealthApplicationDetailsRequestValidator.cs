using FluentValidation;
using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Validators;

public class UpsertHealthApplicationDetailsRequestValidator : AbstractValidator<UpsertHealthApplicationDetailsRequest>
{
    public UpsertHealthApplicationDetailsRequestValidator()
    {
        RuleFor(x => x.ApplicantAge).InclusiveBetween(0, 150).When(x => x.ApplicantAge is not null);
    }
}
