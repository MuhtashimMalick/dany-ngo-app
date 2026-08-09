namespace NgoFund.Domain.Exceptions;

public sealed class RoleNotFoundException(string roleName) : DomainException($"Role '{roleName}' does not exist.");
