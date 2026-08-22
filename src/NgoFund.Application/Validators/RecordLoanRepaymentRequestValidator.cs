using FluentValidation;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Loans;
using NgoFund.Domain.Enums;

namespace NgoFund.Application.Validators;

public class RecordLoanRepaymentRequestValidator : AbstractValidator<RecordLoanRepaymentRequest>
{
    public RecordLoanRepaymentRequestValidator()
    {
        RuleFor(x => x.LoanAgreementId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.RepaymentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.PaymentMethod).NotEmpty()
            .Must(v => Enum.TryParse<PaymentMethod>(v, out var method) && method != PaymentMethod.InKind)
            .WithMessage("PaymentMethod must be one of: Cash, BankTransfer, Cheque, Online.");
        RuleFor(x => x.InstrumentNumber).MaximumLength(100);
        RuleFor(x => x.BankName).MaximumLength(150);
        RuleFor(x => x.ReceivedFromName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ReceivedFromCnic).Matches(PakistaniFormats.CnicPattern)
            .WithMessage(PakistaniFormats.CnicMessage)
            .When(x => !string.IsNullOrEmpty(x.ReceivedFromCnic));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
