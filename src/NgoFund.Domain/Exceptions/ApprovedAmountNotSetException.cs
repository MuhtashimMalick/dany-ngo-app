namespace NgoFund.Domain.Exceptions;

public sealed class ApprovedAmountNotSetException()
    : DomainException("This application has no approved amount set and cannot accept payments.");
