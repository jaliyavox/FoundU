namespace FoundU.Application.Common.Exceptions;

/// <summary>Maps to HTTP 401 - missing/invalid credentials (e.g. bad login, expired refresh token).</summary>
public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Invalid credentials.") : base(message)
    {
    }
}
