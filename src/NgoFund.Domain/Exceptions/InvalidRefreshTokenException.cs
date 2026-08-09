namespace NgoFund.Domain.Exceptions;

/// <summary>The refresh token is missing, expired, already revoked, or already rotated (reuse detected).</summary>
public sealed class InvalidRefreshTokenException() : DomainException("The refresh token is invalid or has expired.");
