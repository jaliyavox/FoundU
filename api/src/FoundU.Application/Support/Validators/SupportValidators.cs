using FluentValidation;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Enums;

namespace FoundU.Application.Support.Validators;

public class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequest>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Give it a subject - one line is enough.")
            .MaximumLength(200);

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(value => Enum.TryParse<SupportTicketCategory>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Choose one of the listed categories.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Say what happened.")
            .MinimumLength(10).WithMessage("A sentence or two, so the desk does not have to ask.")
            .MaximumLength(4000);

        RuleFor(x => x.RelatedEntityType).MaximumLength(60);
    }
}

public class SupportTicketReplyRequestValidator : AbstractValidator<SupportTicketReplyRequest>
{
    public SupportTicketReplyRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Write a message first.")
            .MaximumLength(4000);
    }
}

public class UpdateSupportTicketRequestValidator : AbstractValidator<UpdateSupportTicketRequest>
{
    public UpdateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(value => Enum.TryParse<SupportTicketStatus>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Unknown status.");
    }
}

public class SupportAssistantRequestValidator : AbstractValidator<SupportAssistantRequest>
{
    public SupportAssistantRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.LastTopic).MaximumLength(40);
        RuleFor(x => x.History).Must(history => history is null || history.Count <= 20)
            .WithMessage("That conversation is long - start again, or send it to the desk.");
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(t => t.Role).Must(role => role is "user" or "assistant");
            turn.RuleFor(t => t.Text).NotEmpty().MaximumLength(1000);
        });
    }
}
