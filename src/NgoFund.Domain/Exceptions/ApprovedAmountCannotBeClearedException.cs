namespace NgoFund.Domain.Exceptions;

public sealed class ApprovedAmountCannotBeClearedException()
    : DomainException("The approved amount cannot be cleared once the application has been approved.");
