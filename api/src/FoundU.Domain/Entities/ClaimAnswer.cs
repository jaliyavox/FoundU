using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>The student's answer to a VerificationQuestion, plus the deterministic correctness check result.</summary>
public class ClaimAnswer : BaseEntity
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = default!;

    public Guid VerificationQuestionId { get; set; }
    public VerificationQuestion VerificationQuestion { get; set; } = default!;

    public string AnswerText { get; set; } = default!;

    /// <summary>Null until the Verification Agent / deterministic rule has evaluated the answer.</summary>
    public bool? IsCorrect { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
