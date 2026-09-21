using FluentValidation;
using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Validators;

public class ApproveGuarantorConflictOverrideRequestValidator : AbstractValidator<ApproveGuarantorConflictOverrideRequest>
{
    public ApproveGuarantorConflictOverrideRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
