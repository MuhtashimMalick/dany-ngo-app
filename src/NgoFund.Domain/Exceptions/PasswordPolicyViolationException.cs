namespace NgoFund.Domain.Exceptions;

public sealed class PasswordPolicyViolationException(string details) : DomainException(details);
