using FluentValidation;
using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.LostReports.Validators;

public class CreateLostReportRequestValidator : AbstractValidator<CreateLostReportRequest>
{
    public CreateLostReportRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.ItemTypeId).NotEmpty();
        RuleFor(x => x.LastSeenLocationId).NotEmpty();

        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Describe the item in at least 10 characters so it can be matched.")
            .MaximumLength(2000);

        RuleFor(x => x.PrimaryColor).MaximumLength(50);
        RuleFor(x => x.SecondaryColor).MaximumLength(50);

        RuleFor(x => x.EstimatedLostFromAt)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("The start of the time window cannot be in the future.");

        // Mirrors the database check constraint on LostReports.
        RuleFor(x => x.EstimatedLostToAt)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.EstimatedLostFromAt)
            .WithMessage("The end of the time window must be on or after the start.");
    }
}
