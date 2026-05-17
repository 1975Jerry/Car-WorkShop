namespace Workshop.Application.Common.Notifications;

/// <summary>
/// Outbound email channel. Implementations: SMTP, SendGrid, log-only stub.
/// Implementations must not throw on transient failure — wrap delivery errors and log instead,
/// so a downstream channel failure never poisons the originating business transaction.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Outbound SMS channel. Implementations: Twilio, AWS SNS, log-only stub.
/// Same failure-isolation contract as <see cref="IEmailSender"/>.
/// </summary>
public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken ct = default);
}

public record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string? PlainTextBody = null);

public record SmsMessage(string ToPhone, string Body);
