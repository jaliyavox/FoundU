using FluentValidation;
using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Auth.Validators;

public class RevokeTokenRequestValidator : AbstractValidator<RevokeTokenRequest>
{
    public RevokeTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
