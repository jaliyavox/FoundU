using FluentValidation;
using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.LostReports.Validators;

public class SendLostReportMessageRequestValidator : AbstractValidator<SendLostReportMessageRequest>
{
    public SendLostReportMessageRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty()
            .MinimumLength(5).WithMessage("Say a little more so the owner knows what you mean.")
            .MaximumLength(1000);
    }
}
