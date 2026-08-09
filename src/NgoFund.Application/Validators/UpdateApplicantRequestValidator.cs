using FluentValidation;
using NgoFund.Contracts.Applicants;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class UpdateApplicantRequestValidator : AbstractValidator<UpdateApplicantRequest>
{
    public UpdateApplicantRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnic).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Gender).NotEmpty().Must(v => Enum.TryParse<Gender>(v, out _))
            .WithMessage($"Gender must be one of: {string.Join(", ", Enum.GetNames<Gender>())}.");
        RuleFor(x => x.MaritalStatus).Must(v => v is null || Enum.TryParse<MaritalStatus>(v, out _))
            .WithMessage($"MaritalStatus must be one of: {string.Join(", ", Enum.GetNames<MaritalStatus>())}.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.MembershipNumber).MaximumLength(30);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.BlacklistReason).MaximumLength(500);
    }
}
