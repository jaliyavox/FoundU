using FluentValidation;

namespace FoundU.Application.Intake;

public class IntakeRequestValidator : AbstractValidator<IntakeRequest>
{
    public IntakeRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        When(x => x.Slots != null, () =>
        {
            RuleFor(x => x.Slots!.ItemType).MaximumLength(80);
            RuleFor(x => x.Slots!.Colour).MaximumLength(40);
            RuleFor(x => x.Slots!.Location).MaximumLength(80);
            RuleFor(x => x.Slots!.When).MaximumLength(80);
            RuleFor(x => x.Slots!.Distinctive).MaximumLength(200);
            RuleFor(x => x.Slots!.Intent).Must(IntakeSlots.IsValidIntent)
                .WithMessage("Intent is either 'lost' or 'found'.");
        });
    }
}
