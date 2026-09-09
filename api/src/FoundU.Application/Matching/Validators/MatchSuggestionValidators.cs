using FluentValidation;
using FoundU.Application.Matching.Dtos;

namespace FoundU.Application.Matching.Validators;

public class CreateMatchSuggestionRequestValidator : AbstractValidator<CreateMatchSuggestionRequest>
{
    public CreateMatchSuggestionRequestValidator()
    {
        RuleFor(x => x.LostReportId).NotEmpty();
        RuleFor(x => x.FoundReportId).NotEmpty();

        // The note reaches the student, so it is a message, not an internal jotting.
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class DismissMatchSuggestionRequestValidator : AbstractValidator<DismissMatchSuggestionRequest>
{
    public DismissMatchSuggestionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
