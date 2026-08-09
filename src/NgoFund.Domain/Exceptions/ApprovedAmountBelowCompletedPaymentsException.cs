namespace NgoFund.Domain.Exceptions;

public sealed class ApprovedAmountBelowCompletedPaymentsException(decimal? newApprovedAmount, decimal completedPaid)
    : DomainException(
        newApprovedAmount is null
            ? $"Cannot clear the approved amount: {completedPaid:N2} has already been paid against this application."
            : $"Cannot set approved amount to {newApprovedAmount.Value:N2}: {completedPaid:N2} has already been paid against this application.");
