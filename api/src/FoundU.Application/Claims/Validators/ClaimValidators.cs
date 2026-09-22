using FluentValidation;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common;
using FoundU.Domain.Common;

namespace FoundU.Application.Claims.Validators;

public class CreateClaimRequestValidator : AbstractValidator<CreateClaimRequest>
{
    public CreateClaimRequestValidator()
    {
        RuleFor(x => x.LostReportId).NotEmpty();
        RuleFor(x => x.FoundReportId).NotEmpty();
    }
}

public class AddVerificationQuestionsRequestValidator : AbstractValidator<AddVerificationQuestionsRequest>
{
    public AddVerificationQuestionsRequestValidator()
    {
        RuleFor(x => x.Questions)
            .NotEmpty().WithMessage("Add at least one question.")
            .Must(q => q.Count <= 5).WithMessage("Five questions is the most a claimant should face at once.");

        RuleForEach(x => x.Questions)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Ask something specific enough that only the owner could answer it.")
            .MaximumLength(500);
    }
}

public class SubmitClaimAnswersRequestValidator : AbstractValidator<SubmitClaimAnswersRequest>
{
    public SubmitClaimAnswersRequestValidator()
    {
        RuleFor(x => x.Answers).NotEmpty().WithMessage("Answer the questions before submitting.");

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId).NotEmpty();
            answer.RuleFor(a => a.AnswerText)
                .NotEmpty()
                .MaximumLength(1000);
        });
    }
}

public class ClaimDecisionRequestValidator : AbstractValidator<ClaimDecisionRequest>
{
    private static readonly string[] Allowed = ["Approved", "Rejected", "RevisionRequested"];

    public ClaimDecisionRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => Allowed.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Decision must be one of: {string.Join(", ", Allowed)}.");

        RuleFor(x => x.Reason).MaximumLength(1000);

        // Approving needs no explanation; the two outcomes that cost the claimant something
        // do, and it is shown to them.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => !string.Equals(x.Decision, "Approved", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Say why - the claimant is shown this.");
    }
}

public class OverturnClaimRequestValidator : AbstractValidator<OverturnClaimRequest>
{
    public OverturnClaimRequestValidator()
    {
        // An override is recorded against the admin by name, and the staff member whose call
        // it reverses will read it. It has to say why.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why the rejection is being overturned.")
            .MinimumLength(10).WithMessage("Say enough that the staff member who rejected it understands.")
            .MaximumLength(1000);
    }
}

public class CollectClaimRequestValidator : AbstractValidator<CollectClaimRequest>
{
    public CollectClaimRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Ask the student for their collection code.")
            .Must(c => HandoverCodes.LooksValid(c.Replace(" ", "")))
            .WithMessage("A collection code is six digits.");
    }
}
