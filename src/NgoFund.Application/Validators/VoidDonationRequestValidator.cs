using FluentValidation;
using NgoFund.Contracts.Donations;

namespace NgoFund.Application.Validators;

public class VoidDonationRequestValidator : AbstractValidator<VoidDonationRequest>
{
    public VoidDonationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
