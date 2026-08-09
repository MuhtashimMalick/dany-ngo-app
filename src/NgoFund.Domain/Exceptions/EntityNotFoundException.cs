namespace NgoFund.Domain.Exceptions;

public sealed class EntityNotFoundException(string entityName, object id) : DomainException($"{entityName} '{id}' was not found.");
