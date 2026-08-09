namespace NgoFund.Domain.Exceptions;

public sealed class UserInactiveException() : DomainException("This user account has been deactivated.");
