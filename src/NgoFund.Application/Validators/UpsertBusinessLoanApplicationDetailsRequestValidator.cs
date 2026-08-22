using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Common;

namespace NgoFund.Application.Validators;

public class UpsertBusinessLoanApplicationDetailsRequestValidator : AbstractValidator<UpsertBusinessLoanApplicationDetailsRequest>
{
    public UpsertBusinessLoanApplicationDetailsRequestValidator()
    {
        RuleFor(x => x.ProposedBusinessDescription).NotEmpty();
        RuleFor(x => x.CapitalRequired).GreaterThanOrEqualTo(0).When(x => x.CapitalRequired is not null);
        RuleFor(x => x.CapitalAlreadyAvailable).GreaterThanOrEqualTo(0).When(x => x.CapitalAlreadyAvailable is not null);
        RuleFor(x => x.TotalMonthlyExpenses).GreaterThanOrEqualTo(0).When(x => x.TotalMonthlyExpenses is not null);
        RuleFor(x => x.EmergencyContactCnic).MaximumLength(15).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage)
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactCnic));
    }
}
