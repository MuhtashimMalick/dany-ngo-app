using FluentValidation;
using NgoFund.Contracts.Donors;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreateDonorRequestValidator : AbstractValidator<CreateDonorRequest>
{
    public CreateDonorRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DonorType).NotEmpty().Must(v => Enum.TryParse<DonorType>(v, out _))
            .WithMessage($"DonorType must be one of: {string.Join(", ", Enum.GetNames<DonorType>())}.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Cnic).MaximumLength(20);
        RuleFor(x => x.Ntn).MaximumLength(30);
        RuleFor(x => x.MembershipNumber).MaximumLength(30);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}
