using FluentValidation;
using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.LostReports.Validators;

/// <summary>
/// A flag asks a staff member to spend time on a report, so it has to say why. Without this
/// an empty body reached the service and "Reason".Trim() threw a 500.
/// </summary>
public class FlagLostReportRequestValidator : AbstractValidator<FlagLostReportRequest>
{
    public FlagLostReportRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say what staff should look at.")
            .MinimumLength(5).WithMessage("Say a little more so staff know what to look for.")
            .MaximumLength(500);

        RuleFor(x => x.FlagType).MaximumLength(50);
    }
}
