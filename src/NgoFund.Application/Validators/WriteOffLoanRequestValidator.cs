using FluentValidation;
using NgoFund.Contracts.Loans;

namespace NgoFund.Application.Validators;

public class WriteOffLoanRequestValidator : AbstractValidator<WriteOffLoanRequest>
{
    public WriteOffLoanRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
