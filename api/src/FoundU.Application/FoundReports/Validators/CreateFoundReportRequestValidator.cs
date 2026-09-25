using FluentValidation;
using FoundU.Application.FoundReports.Dtos;

namespace FoundU.Application.FoundReports.Validators;

public class CreateFoundReportRequestValidator : AbstractValidator<CreateFoundReportRequest>
{
    public CreateFoundReportRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.ItemTypeId).NotEmpty();
        RuleFor(x => x.FoundLocationId).NotEmpty();
        RuleFor(x => x.StorageLocationId).NotEmpty();

        RuleFor(x => x.GeneralDescription)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Describe the item in at least 10 characters so it can be matched.")
            .MaximumLength(2000);

        RuleFor(x => x.PrivateVerificationDetails)
            .MaximumLength(2000);

        RuleFor(x => x.PrimaryColor).MaximumLength(50);
        RuleFor(x => x.SecondaryColor).MaximumLength(50);

        // An item cannot have been found in the future. Small skew allowance for clients
        // whose clock runs slightly ahead of the server.
        RuleFor(x => x.FoundAt)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("The found date cannot be in the future.");
    }
}
