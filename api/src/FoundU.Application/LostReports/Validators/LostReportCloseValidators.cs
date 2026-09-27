using FluentValidation;
using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.LostReports.Validators;

/// <summary>Both notes land in a 500-character column; longer text would fail at the database.</summary>
public class WithdrawLostReportRequestValidator : AbstractValidator<WithdrawLostReportRequest>
{
    public WithdrawLostReportRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class ResolveLostReportRequestValidator : AbstractValidator<ResolveLostReportRequest>
{
    public ResolveLostReportRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
