using FluentValidation;
using NgoFund.Contracts.Donations;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreateDonationRequestValidator : AbstractValidator<CreateDonationRequest>
{
    public CreateDonationRequestValidator()
    {
        RuleFor(x => x.DonorId).NotEmpty();
        RuleFor(x => x.FundCategoryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.DonationDate).NotEqual(default(DateOnly));
        RuleFor(x => x.PaymentMethod).NotEmpty().Must(v => Enum.TryParse<PaymentMethod>(v, out _))
            .WithMessage($"PaymentMethod must be one of: {string.Join(", ", Enum.GetNames<PaymentMethod>())}.");
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.InstrumentNumber).MaximumLength(100);
    }
}
