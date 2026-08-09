using FluentValidation;
using NgoFund.Contracts.Payments;

namespace NgoFund.Application.Validators;

public class VoidPaymentRequestValidator : AbstractValidator<VoidPaymentRequest>
{
    public VoidPaymentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
