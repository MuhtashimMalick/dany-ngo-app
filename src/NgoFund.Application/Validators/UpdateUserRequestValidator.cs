using FluentValidation;
using NgoFund.Contracts.Users;

namespace NgoFund.Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Designation).MaximumLength(150);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role must be assigned.");
    }
}
