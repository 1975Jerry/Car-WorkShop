using Microsoft.Extensions.Logging;
using Workshop.Application.Common.Notifications;

namespace Workshop.Infrastructure.Notifications;

/// <summary>
/// Phase 11 placeholder. Logs the message instead of delivering it.
/// Swap with a Twilio / Vonage / AWS SNS adapter when a gateway is configured.
/// </summary>
public class LoggingSmsSender(ILogger<LoggingSmsSender> log) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        log.LogInformation("[SMS→{ToPhone}] {Body}", message.ToPhone, message.Body);
        return Task.CompletedTask;
    }
}
