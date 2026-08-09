namespace NgoFund.Domain.Exceptions;

public sealed class PaymentExceedsApprovedAmountException(decimal approvedAmount, decimal alreadyPaid, decimal attemptedPayment)
    : DomainException(
        $"Payment of {attemptedPayment:N2} would bring total paid to {alreadyPaid + attemptedPayment:N2}, exceeding the approved amount of {approvedAmount:N2}.");
