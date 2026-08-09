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
        RuleFor(x => x.RequestedAmount).GreaterThan(0);
        RuleFor(x => x.ApprovedAmount).LessThanOrEqualTo(x => x.RequestedAmount).When(x => x.ApprovedAmount is not null)
            .WithMessage("Approved amount cannot exceed the requested amount.");
        RuleFor(x => x.Priority).NotEmpty().Must(v => Enum.TryParse<ApplicationPriority>(v, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<ApplicationPriority>())}.");
        RuleFor(x => x.Purpose).MaximumLength(1000);
    }
}
