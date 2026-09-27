using FluentValidation;
using FoundU.Application.Admin.Dtos;

namespace FoundU.Application.Admin.Validators;

public class ReferenceItemRequestValidator : AbstractValidator<ReferenceItemRequest>
{
    public ReferenceItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give it a name.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Building).MaximumLength(150);

        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0).When(x => x.Capacity is not null)
            .WithMessage("Capacity cannot be negative.");
    }
}
