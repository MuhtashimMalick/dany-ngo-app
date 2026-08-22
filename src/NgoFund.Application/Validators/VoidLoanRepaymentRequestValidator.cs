using FluentValidation;
using NgoFund.Contracts.Loans;

namespace NgoFund.Application.Validators;

public class VoidLoanRepaymentRequestValidator : AbstractValidator<VoidLoanRepaymentRequest>
{
    public VoidLoanRepaymentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
