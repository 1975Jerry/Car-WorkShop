namespace Workshop.Application.Common.Notifications;

/// <summary>
/// High-level fan-out: persists an in-app Notification row for each recipient
/// and forwards to email + SMS channels per recipient preferences. Use this
/// from handlers — do not call IEmailSender / ISmsSender directly.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationRequest request, CancellationToken ct = default);
}

/// <summary>
/// A single business event, fanned out to one or more recipients.
/// Recipients are resolved by the dispatcher into in-app rows and channel deliveries.
/// </summary>
public record NotificationRequest(
    NotificationKind Kind,
    string TitleGr,
    string TitleEn,
    string BodyGr,
    string BodyEn,
    string? Url,
    IReadOnlyList<NotificationRecipient> Recipients);

public record NotificationRecipient(
    Guid UserId,
    string? Email,
    string? Phone,
    string Language,
    NotificationChannels Channels);

[Flags]
public enum NotificationChannels
{
    None = 0,
    InApp = 1,
    Email = 2,
    Sms = 4,
    All = InApp | Email | Sms,
}

public enum NotificationKind
{
    CaseStatusChanged,
    QuoteIssued,
    RepairScheduled,
    RepairCompleted,
    PaymentRecorded,
    PartOrdered,
    PartReceived,
    InsuranceApprovalDecision,
    SupplierDispatch,
}
