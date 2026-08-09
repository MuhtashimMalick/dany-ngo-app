namespace NgoFund.Domain.Exceptions;

public sealed class AccountLockedOutException()
    : DomainException("This account is temporarily locked due to too many failed sign-in attempts. Try again later.");
