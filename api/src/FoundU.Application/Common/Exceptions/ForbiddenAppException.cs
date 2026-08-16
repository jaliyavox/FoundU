namespace FoundU.Application.Common.Exceptions;

/// <summary>Maps to HTTP 403 - authenticated, but not allowed to perform this action.</summary>
public class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "You are not allowed to perform this action.")
        : base(message)
    {
    }
}
