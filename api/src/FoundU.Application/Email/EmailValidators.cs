using FluentValidation;

namespace FoundU.Application.Email;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Enter the email you signed up with.")
            .EmailAddress().WithMessage("That doesn't look like an email address.").MaximumLength(256);
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
        // Identity enforces the full password rules; these give the first answer quickly.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("At least 8 characters, with an upper-case letter, a lower-case letter and a number.")
            .MaximumLength(128);
    }
}

public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
    }
}
