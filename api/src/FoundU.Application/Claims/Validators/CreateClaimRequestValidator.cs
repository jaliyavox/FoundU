using FluentValidation;
using FoundU.Application.Claims.Dtos;

namespace FoundU.Application.Claims.Validators;

public class CreateClaimRequestValidator : AbstractValidator<CreateClaimRequest>
{
    public CreateClaimRequestValidator()
    {
        RuleFor(x => x.LostReportId).NotEmpty();
        RuleFor(x => x.FoundReportId).NotEmpty();
    }
}
