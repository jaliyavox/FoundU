using FluentValidation;
using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        // Mirrors the password policy configured on IdentityOptions.Password in Program.cs -
        // kept here too so the client gets a fast, clear 400 instead of a generic Identity error.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.StudentNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.StudentNumber));
    }
}
