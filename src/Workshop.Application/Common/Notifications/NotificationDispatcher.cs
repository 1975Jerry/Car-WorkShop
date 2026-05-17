using Microsoft.Extensions.Logging;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.CrossCutting;

namespace Workshop.Application.Common.Notifications;

public class NotificationDispatcher(
    IWorkshopDbContext db,
    IEmailSender email,
    ISmsSender sms,
    TimeProvider clock,
    ILogger<NotificationDispatcher> log)
    : INotificationDispatcher
{
    public async Task DispatchAsync(NotificationRequest request, CancellationToken ct = default)
    {
        if (request.Recipients.Count == 0)
            return;

        var now = clock.GetUtcNow().UtcDateTime;

        foreach (var recipient in request.Recipients)
        {
            var (title, body) = LocalizedTextFor(request, recipient.Language);

            if (recipient.Channels.HasFlag(NotificationChannels.InApp))
            {
                db.Notifications.Add(new Notification
                {
                    UserId = recipient.UserId,
                    Title = title,
                    Body = body,
                    Url = request.Url,
                    IsRead = false,
                    OccurredAt = now,
                });
            }

            if (recipient.Channels.HasFlag(NotificationChannels.Email) && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                await TrySendAsync(
                    () => email.SendAsync(new EmailMessage(recipient.Email!, title, title, body), ct),
                    "email", recipient.UserId, request.Kind);
            }

            if (recipient.Channels.HasFlag(NotificationChannels.Sms) && !string.IsNullOrWhiteSpace(recipient.Phone))
            {
                await TrySendAsync(
                    () => sms.SendAsync(new SmsMessage(recipient.Phone!, $"{title}: {body}"), ct),
                    "sms", recipient.UserId, request.Kind);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task TrySendAsync(Func<Task> send, string channel, Guid userId, NotificationKind kind)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Notification {Channel} delivery failed for user {UserId} kind {Kind}", channel, userId, kind);
        }
    }

    private static (string Title, string Body) LocalizedTextFor(NotificationRequest r, string language)
        => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? (r.TitleEn, r.BodyEn)
            : (r.TitleGr, r.BodyGr);
}
