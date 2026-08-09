namespace NgoFund.Domain.Exceptions;

public sealed class DuplicateEmailException(string email) : DomainException($"A user with email '{email}' already exists.");
