using System.Net;

namespace NgoFund.Desktop.Services;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ApiException(HttpStatusCode statusCode, string message)
        : this(statusCode, message, null)
    {
    }

    public ApiException(HttpStatusCode statusCode, string message, IReadOnlyDictionary<string, string[]>? errors)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}

internal record ProblemDetailsLite(string? Title, string? Detail, IReadOnlyDictionary<string, string[]>? Errors);
