using FluentValidation;
using NgoFund.Contracts.Auth;

namespace NgoFund.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        // Length only — the actual password policy (digit/upper/lower required) lives in one
        // place, ASP.NET Identity's PasswordOptions (see AddInfrastructure), and is enforced by
        // UserManager itself. Duplicating the full policy here would just drift out of sync.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from the current password.");
    }
}
