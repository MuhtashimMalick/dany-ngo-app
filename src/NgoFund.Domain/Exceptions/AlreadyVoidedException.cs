namespace NgoFund.Domain.Exceptions;

public sealed class AlreadyVoidedException(string entityName) : DomainException($"This {entityName} has already been voided.");
