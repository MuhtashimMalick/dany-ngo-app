using NgoFund.Domain.Enums;

namespace NgoFund.Domain.Exceptions;

public sealed class InvalidStatusTransitionException(ApplicationStatus from, ApplicationStatus to)
    : DomainException($"Cannot transition an application from '{from}' to '{to}'.");
