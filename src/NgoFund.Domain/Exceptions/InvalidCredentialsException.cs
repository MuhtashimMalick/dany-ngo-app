namespace NgoFund.Domain.Exceptions;

/// <summary>Wrong email/password at login, or wrong current password when changing password.</summary>
public sealed class InvalidCredentialsException() : DomainException("The credentials provided are invalid.");
