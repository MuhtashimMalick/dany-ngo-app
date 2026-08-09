using FluentValidation;
using NgoFund.Contracts.Payments;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.PaymentMethod).NotEmpty()
            .Must(v => Enum.TryParse<PaymentMethod>(v, out var method) && method != PaymentMethod.InKind)
            .WithMessage("PaymentMethod must be one of: Cash, BankTransfer, Cheque, Online.");
        RuleFor(x => x.InstrumentNumber).MaximumLength(100);
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.PaidToName).MaximumLength(200);
        RuleFor(x => x.PaidToCnic).MaximumLength(20);
        RuleFor(x => x.PaidToRelation).MaximumLength(100);
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
