using FluentValidation;
using NgoFund.Contracts.Applications;

namespace NgoFund.Application.Validators;

public class AddRemarkRequestValidator : AbstractValidator<AddRemarkRequest>
{
    public AddRemarkRequestValidator()
    {
        RuleFor(x => x.Remark).NotEmpty().MaximumLength(2000);
    }
}
