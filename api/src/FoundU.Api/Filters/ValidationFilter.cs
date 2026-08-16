using FluentValidation;
using FoundU.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FoundU.Api.Filters;

/// <summary>
/// Runs before every controller action. For each action argument that has a registered
/// FluentValidation IValidator&lt;T&gt;, validates it and throws ValidationAppException (400,
/// field-level errors) on failure - GlobalExceptionHandler turns that into the standard
/// ProblemDetails envelope. Registered globally in Program.cs so no controller/action needs to
/// remember to call it manually.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue; // no validator registered for this DTO type - nothing to check
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray());

                throw new ValidationAppException(errors);
            }
        }

        await next();
    }
}
