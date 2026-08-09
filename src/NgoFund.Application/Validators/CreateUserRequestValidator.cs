using FluentValidation;
using NgoFund.Contracts.Users;

namespace NgoFund.Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Designation).MaximumLength(150);
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role must be assigned.");
    }
}
