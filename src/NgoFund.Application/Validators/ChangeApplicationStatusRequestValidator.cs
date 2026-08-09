using FluentValidation;
using NgoFund.Contracts.Applications;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class ChangeApplicationStatusRequestValidator : AbstractValidator<ChangeApplicationStatusRequest>
{
    public ChangeApplicationStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus).NotEmpty().Must(v => Enum.TryParse<ApplicationStatus>(v, out _))
            .WithMessage($"NewStatus must be one of: {string.Join(", ", Enum.GetNames<ApplicationStatus>())}.");

        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500)
            .When(x => x.NewStatus == nameof(ApplicationStatus.Rejected))
            .WithMessage("A rejection reason is required when rejecting an application.");

        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
