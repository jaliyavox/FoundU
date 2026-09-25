namespace FoundU.Application.Common.Exceptions;

/// <summary>Maps to HTTP 404.</summary>
public class NotFoundAppException : AppException
{
    public NotFoundAppException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }

    public NotFoundAppException(string message) : base(message)
    {
    }
}
