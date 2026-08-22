using FluentValidation;
using NgoFund.Contracts.Loans;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class PreviewLoanScheduleRequestValidator : AbstractValidator<PreviewLoanScheduleRequest>
{
    public PreviewLoanScheduleRequestValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.InstallmentCount).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Frequency).NotEmpty().Must(v => Enum.TryParse<LoanInstallmentFrequency>(v, out _))
            .WithMessage($"Frequency must be one of: {string.Join(", ", Enum.GetNames<LoanInstallmentFrequency>())}.");
    }
}
