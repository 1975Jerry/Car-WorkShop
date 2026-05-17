using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workshop.Application.Common.Notifications;

namespace Workshop.Application.Tests;

public class NotificationDispatcherTests
{
    [Fact]
    public async Task Dispatch_writes_in_app_row_per_recipient_and_calls_channels()
    {
        await using var db = TestDb.NewContext();
        var email = new RecordingEmailSender();
        var sms = new RecordingSmsSender();
        var dispatcher = new NotificationDispatcher(db, email, sms, TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await dispatcher.DispatchAsync(new NotificationRequest(
            Kind: NotificationKind.QuoteIssued,
            TitleGr: "Νέα Προσφορά",
            TitleEn: "New quote",
            BodyGr: "Σώμα EL",
            BodyEn: "Body EN",
            Url: "/cases/insurance/abc",
            Recipients:
            [
                new NotificationRecipient(alice, "alice@example.com", "+306900000001", "el", NotificationChannels.All),
                new NotificationRecipient(bob,   "bob@example.com",   "+306900000002", "en", NotificationChannels.InApp | NotificationChannels.Email),
            ]));

        var rows = await db.Notifications.AsNoTracking().OrderBy(n => n.UserId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.UserId == alice && r.Title == "Νέα Προσφορά" && r.Body == "Σώμα EL");
        Assert.Contains(rows, r => r.UserId == bob && r.Title == "New quote" && r.Body == "Body EN");
        Assert.All(rows, r => Assert.False(r.IsRead));

        Assert.Equal(2, email.Sent.Count);
        Assert.Single(sms.Sent);
        Assert.Equal("+306900000001", sms.Sent[0].ToPhone);
    }

    [Fact]
    public async Task Dispatch_swallows_channel_failures_so_in_app_row_still_persists()
    {
        await using var db = TestDb.NewContext();
        var email = new ThrowingEmailSender();
        var sms = new RecordingSmsSender();
        var dispatcher = new NotificationDispatcher(db, email, sms, TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        var user = Guid.NewGuid();
        await dispatcher.DispatchAsync(new NotificationRequest(
            Kind: NotificationKind.CaseStatusChanged,
            TitleGr: "Τ", TitleEn: "T",
            BodyGr: "Β", BodyEn: "B",
            Url: null,
            Recipients: [new NotificationRecipient(user, "x@example.com", null, "el", NotificationChannels.InApp | NotificationChannels.Email)]));

        Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Dispatch_with_no_recipients_is_a_no_op()
    {
        await using var db = TestDb.NewContext();
        var dispatcher = new NotificationDispatcher(db,
            new RecordingEmailSender(), new RecordingSmsSender(), TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(new NotificationRequest(
            NotificationKind.PaymentRecorded, "t", "t", "b", "b", null, Array.Empty<NotificationRecipient>()));

        Assert.Empty(await db.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Dispatch_skips_email_channel_when_address_missing()
    {
        await using var db = TestDb.NewContext();
        var email = new RecordingEmailSender();
        var sms = new RecordingSmsSender();
        var dispatcher = new NotificationDispatcher(db, email, sms, TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(new NotificationRequest(
            NotificationKind.RepairScheduled, "t", "t", "b", "b", null,
            [new NotificationRecipient(Guid.NewGuid(), null, null, "el", NotificationChannels.All)]));

        Assert.Empty(email.Sent);
        Assert.Empty(sms.Sent);
        Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
    }

    private class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private class RecordingSmsSender : ISmsSender
    {
        public List<SmsMessage> Sent { get; } = new();
        public Task SendAsync(SmsMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated provider outage");
    }
}
