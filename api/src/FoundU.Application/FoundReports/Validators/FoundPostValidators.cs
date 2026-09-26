using FluentValidation;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Common;

namespace FoundU.Application.FoundReports.Validators;

public class CreateFoundPostRequestValidator : AbstractValidator<CreateFoundPostRequest>
{
    public CreateFoundPostRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.ItemTypeId).NotEmpty();
        RuleFor(x => x.FoundLocationId).NotEmpty();

        // Long enough to recognise, short enough that a finder is not tempted to list every
        // identifying detail - those belong to the desk, not the feed.
        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Say what it is and roughly where - enough for the owner to recognise it.")
            .MaximumLength(500).WithMessage("Keep it short. Anything that proves ownership goes to the desk, not the feed.");

        RuleFor(x => x.PrimaryColor).MaximumLength(50);

        RuleFor(x => x.FoundAt)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("You cannot have found it in the future.");

        RuleFor(x => x.LostReportHandInCode)
            .Must(c => c is null || HandoverCodes.LooksValid(c.Replace(" ", "")))
            .WithMessage("A hand-in code is six digits.");
    }
}

public class ConfirmFoundPostRequestValidator : AbstractValidator<ConfirmFoundPostRequest>
{
    public ConfirmFoundPostRequestValidator()
    {
        RuleFor(x => x.StorageLocationId).NotEmpty();
        RuleFor(x => x.PrivateVerificationDetails).MaximumLength(2000);
        RuleFor(x => x.GeneralDescription).MaximumLength(2000);
    }
}

public class WithdrawFoundPostRequestValidator : AbstractValidator<WithdrawFoundPostRequest>
{
    public WithdrawFoundPostRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class RecogniseFoundPostRequestValidator : AbstractValidator<RecogniseFoundPostRequest>
{
    public RecogniseFoundPostRequestValidator()
    {
        RuleFor(x => x.LostReportId).NotEmpty();
    }
}
