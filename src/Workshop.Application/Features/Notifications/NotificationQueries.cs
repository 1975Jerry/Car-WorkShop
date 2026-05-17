using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Notifications;

public record GetMyNotificationInboxQuery(int Take = 20) : IRequest<NotificationInboxDto>;

public class GetMyNotificationInboxHandler(IWorkshopDbContext db, ICurrentUserService user)
    : IRequestHandler<GetMyNotificationInboxQuery, NotificationInboxDto>
{
    public async Task<NotificationInboxDto> Handle(GetMyNotificationInboxQuery q, CancellationToken ct)
    {
        if (user.UserId is not { } uid)
            return new NotificationInboxDto(0, []);

        var unread = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == uid && !n.IsRead, ct);

        var recent = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == uid)
            .OrderByDescending(n => n.OccurredAt)
            .Take(q.Take)
            .Select(n => new NotificationListItemDto(n.Id, n.Title, n.Body, n.Url, n.IsRead, n.OccurredAt))
            .ToListAsync(ct);

        return new NotificationInboxDto(unread, recent);
    }
}
