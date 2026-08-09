using System.Net;

namespace NgoFund.Desktop.Services;

public class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

internal record ProblemDetailsLite(string? Title, string? Detail);
