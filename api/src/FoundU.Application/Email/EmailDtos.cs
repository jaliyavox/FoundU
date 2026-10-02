namespace FoundU.Application.Email;

/// <summary>One message. Both an HTML and a plain-text body: some clients only show one.</summary>
public record EmailMessage(string To, string Subject, string Html, string Text);

/// <summary>Sends an email. False means it was not sent - callers decide whether that matters.</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>"Forgot password" - always answered the same way, whether or not the address exists.</summary>
public record ForgotPasswordRequest(string Email);

/// <summary>From the link in the reset email: who, the one-time token, and the new password.</summary>
public record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>From the link in the confirmation email.</summary>
public record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>
/// The emails an account needs: a link to reset a forgotten password, and a link that proves
/// the address belongs to the person who typed it.
/// </summary>
public interface IAccountEmailService
{
    /// <summary>
    /// Sends a reset link if the address belongs to an account. Says nothing either way, so
    /// the form cannot be used to find out who has an account.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Sets the new password from a valid link, and signs every other session out.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Sends (or re-sends) the confirmation link to the account's current address.</summary>
    Task SendConfirmationAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default);
}
