using Microsoft.Extensions.Logging;
using Workshop.Application.Common.Notifications;

namespace Workshop.Infrastructure.Notifications;

/// <summary>
/// Phase 11 placeholder. Logs the message instead of delivering it.
/// Swap with an SMTP / SendGrid / SES adapter when delivery credentials are configured.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> log) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        log.LogInformation(
            "[EMAIL→{ToAddress}] {Subject} :: {BodyPreview}",
            message.ToAddress,
            message.Subject,
            Preview(message.PlainTextBody ?? message.HtmlBody));
        return Task.CompletedTask;
    }

    private static string Preview(string body)
        => body.Length <= 200 ? body : body[..200] + "…";
}
