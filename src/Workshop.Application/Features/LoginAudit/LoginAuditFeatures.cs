using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Messaging;
using Workshop.Application.Common.Models;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.LoginAudit;

public record LoginAuditListItemDto(
    Guid Id,
    DateTime OccurredAt,
    LoginAuditEvent Event,
    string Email,
    Guid? UserId,
    string? UserDisplayName,
    PortalAudience? PortalAudience,
    string? IpAddress,
    string? UserAgent,
    string? FailureReason);

public record ListLoginAuditQuery(
    string? Search = null,
    LoginAuditEvent? Event = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<LoginAuditListItemDto>>;

public class ListLoginAuditHandler(IWorkshopDbContext db)
    : IRequestHandler<ListLoginAuditQuery, PagedList<LoginAuditListItemDto>>
{
    public async Task<PagedList<LoginAuditListItemDto>> Handle(ListLoginAuditQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = db.LoginAuditEntries.AsNoTracking();

        if (q.Event.HasValue)
            query = query.Where(x => x.Event == q.Event.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Email.ToLower().Contains(s)
                || (x.IpAddress != null && x.IpAddress.ToLower().Contains(s))
                || (x.User != null && x.User.FullName.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new LoginAuditListItemDto(
                x.Id,
                x.OccurredAt,
                x.Event,
                x.Email,
                x.UserId,
                x.User != null ? x.User.FullName : null,
                x.PortalAudience,
                x.IpAddress,
                x.UserAgent,
                x.FailureReason))
            .ToListAsync(ct);

        return new PagedList<LoginAuditListItemDto>(items, page, size, total);
    }
}
