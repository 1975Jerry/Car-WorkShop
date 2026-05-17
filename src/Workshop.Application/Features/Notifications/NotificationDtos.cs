namespace Workshop.Application.Features.Notifications;

public record NotificationListItemDto(
    Guid Id,
    string Title,
    string Body,
    string? Url,
    bool IsRead,
    DateTime OccurredAt);

public record NotificationInboxDto(
    int UnreadCount,
    IReadOnlyList<NotificationListItemDto> Recent);
