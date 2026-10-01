using FluentValidation;
using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Auth.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Your name cannot be blank.")
            .MaximumLength(120);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(256);

        RuleFor(x => x.StudentNumber).MaximumLength(40);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        // Identity enforces the rest of the policy; this is the part worth saying early.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Use at least 8 characters.")
            .MaximumLength(128);
    }
}

public class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
    }
}
