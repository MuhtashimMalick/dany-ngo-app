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

        // Item 1 (2026-09 feedback): when supplied on an Approved transition, it must be positive.
        // Not required outright (no .When(... && ApprovedAmount is not null) guard would be needed
        // for a NotEmpty rule) — omitting it is legal when re-approving from OnHold, in which case
        // FundApplicationService.ChangeStatusAsync keeps the application's existing approved amount.
        RuleFor(x => x.ApprovedAmount).GreaterThan(0)
            .When(x => x.NewStatus == nameof(ApplicationStatus.Approved) && x.ApprovedAmount is not null)
            .WithMessage("Approved amount must be greater than zero.");

        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
