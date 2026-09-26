using FluentValidation;
using FoundU.Application.Handovers.Dtos;

namespace FoundU.Application.Handovers.Validators;

public class ReceiveHandoverRequestValidator : AbstractValidator<ReceiveHandoverRequest>
{
    public ReceiveHandoverRequestValidator()
    {
        RuleFor(x => x.StorageLocationId).NotEmpty().WithMessage("Say where the item is being kept.");
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public class ReleaseHandoverRequestValidator : AbstractValidator<ReleaseHandoverRequest>
{
    public ReleaseHandoverRequestValidator()
    {
        RuleFor(x => x.OwnerIdChecked)
            .Equal(true)
            .WithMessage("Check the collector's student ID against the owner's name before releasing the item.");

        RuleFor(x => x.Note).MaximumLength(200);
    }
}
