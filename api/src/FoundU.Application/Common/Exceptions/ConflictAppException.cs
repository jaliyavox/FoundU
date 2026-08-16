namespace FoundU.Application.Common.Exceptions;

/// <summary>Maps to HTTP 409 - the request conflicts with the current state (e.g. duplicate email).</summary>
public class ConflictAppException : AppException
{
    public ConflictAppException(string message) : base(message)
    {
    }
}
