using FluentValidation;
using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Validators;

public class ApproveApplicantGuarantorConflictOverrideRequestValidator : AbstractValidator<ApproveApplicantGuarantorConflictOverrideRequest>
{
    public ApproveApplicantGuarantorConflictOverrideRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
