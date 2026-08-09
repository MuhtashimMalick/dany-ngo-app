namespace NgoFund.Domain.Exceptions;

public sealed class DuplicateFieldException(string entityName, string fieldName, string value)
    : DomainException($"A {entityName} with {fieldName} '{value}' already exists.");
