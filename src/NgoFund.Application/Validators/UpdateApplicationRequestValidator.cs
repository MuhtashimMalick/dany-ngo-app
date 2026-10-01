using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class UpdateApplicationRequestValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationRequestValidator()
    {
        RuleFor(x => x.ApplicationCategoryId).NotEmpty();
        RuleFor(x => x.FundCategoryId).NotEmpty();
        RuleFor(x => x.RequestedAmount).NotNull().GreaterThan(0);
        // v1.8 correction: the requested amount is NOT a ceiling — the elders' committee may approve
        // less than, equal to, or more than what was requested. Only positivity is validated here;
        // the "never below completed payments"/"never cleared" guards live in FundApplicationService.
        RuleFor(x => x.ApprovedAmount).GreaterThan(0).When(x => x.ApprovedAmount is not null);
        RuleFor(x => x.Priority).NotEmpty().Must(v => Enum.TryParse<ApplicationPriority>(v, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<ApplicationPriority>())}.");
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(1000);
    }
}
