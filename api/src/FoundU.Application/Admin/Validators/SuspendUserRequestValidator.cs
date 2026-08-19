using FluentValidation;
using FoundU.Application.Admin.Dtos;

namespace FoundU.Application.Admin.Validators;

public class SuspendUserRequestValidator : AbstractValidator<SuspendUserRequest>
{
    public SuspendUserRequestValidator()
    {
        // A reason is required, not optional: suspension locks someone out of the system,
        // and the record needs to say why long after the admin who did it has forgotten.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Give a reason - it is recorded against the account.")
            .MinimumLength(10).WithMessage("Give a little more detail than that.")
            .MaximumLength(500);
    }
}
