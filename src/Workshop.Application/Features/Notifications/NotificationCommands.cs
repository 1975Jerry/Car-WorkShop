using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;

namespace Workshop.Application.Features.Notifications;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest;

public class MarkNotificationReadHandler(IWorkshopDbContext db, ICurrentUserService user)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        if (user.UserId is not { } uid)
            return;

        var row = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == cmd.NotificationId && n.UserId == uid, ct);
        if (row is null || row.IsRead)
            return;

        row.IsRead = true;
        await db.SaveChangesAsync(ct);
    }
}

public record MarkAllNotificationsReadCommand : IRequest<int>;

public class MarkAllNotificationsReadHandler(IWorkshopDbContext db, ICurrentUserService user)
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    public async Task<int> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        if (user.UserId is not { } uid)
            return 0;

        var rows = await db.Notifications
            .Where(n => n.UserId == uid && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in rows) n.IsRead = true;
        await db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
