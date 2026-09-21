using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreateApplicationRequestValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationRequestValidator()
    {
        RuleFor(x => x.ApplicantId).NotEmpty();
        RuleFor(x => x.ApplicationCategoryId).NotEmpty();
        RuleFor(x => x.FundCategoryId).NotEmpty();
        RuleFor(x => x.RequestedAmount).GreaterThan(0);
        RuleFor(x => x.Priority).NotEmpty().Must(v => Enum.TryParse<ApplicationPriority>(v, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<ApplicationPriority>())}.");
        RuleFor(x => x.ApplicationDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IntakeChannel).NotEmpty().Must(v => Enum.TryParse<ApplicationIntakeChannel>(v, out _))
            .WithMessage($"IntakeChannel must be one of: {string.Join(", ", Enum.GetNames<ApplicationIntakeChannel>())}.");
        RuleFor(x => x.ExternalFormReference).MaximumLength(100);
    }
}
