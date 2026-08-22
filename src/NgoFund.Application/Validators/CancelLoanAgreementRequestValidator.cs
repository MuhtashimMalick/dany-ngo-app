using FluentValidation;
using NgoFund.Contracts.Loans;

namespace NgoFund.Application.Validators;

public class CancelLoanAgreementRequestValidator : AbstractValidator<CancelLoanAgreementRequest>
{
    public CancelLoanAgreementRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
