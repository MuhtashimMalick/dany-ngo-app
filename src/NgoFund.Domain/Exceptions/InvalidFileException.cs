namespace NgoFund.Domain.Exceptions;

public sealed class InvalidFileException(string reason) : DomainException(reason);
