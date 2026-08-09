using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Api.ExceptionHandling;

/// <summary>
/// The system's one and only error-response mechanism (see ): every
/// <see cref="DomainException"/> raised anywhere in Domain/Application becomes an RFC-9457
/// <c>ProblemDetails</c> response here. No controller should ever hand-roll an error shape.
/// </summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var (status, title) = domainException switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
            InvalidCredentialsException or InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, "Authentication failed."),
            UserInactiveException or AccountLockedOutException => (StatusCodes.Status403Forbidden, "Access denied."),
            _ => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated."),
        };

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainException,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = domainException.Message,
                Type = $"https://ngofund.local/problems/{domainException.GetType().Name}",
            },
        });
    }
}
