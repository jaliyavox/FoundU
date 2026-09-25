namespace FoundU.Application.Common.Exceptions;

/// <summary>
/// Maps to HTTP 400 with a field-level errors dictionary. Thrown by the FluentValidation action
/// filter (FoundU.Api) when a request DTO fails validation, and available for manual use in
/// application services that need to raise a business-rule validation error (e.g. "From must be
/// before To").
/// </summary>
public class ValidationAppException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationAppException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } })
    {
    }
}
