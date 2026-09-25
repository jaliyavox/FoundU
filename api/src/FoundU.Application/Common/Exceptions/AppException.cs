namespace FoundU.Application.Common.Exceptions;

/// <summary>
/// Base type for all application-layer exceptions that GlobalExceptionHandler (FoundU.Api)
/// knows how to translate into a specific ProblemDetails status code. Any exception NOT deriving
/// from this is treated as an unexpected 500 and its details are hidden from the response body.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message)
    {
    }
}
