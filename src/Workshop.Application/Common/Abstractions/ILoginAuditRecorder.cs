using Workshop.Domain.Enums;

namespace Workshop.Application.Common.Abstractions;

public interface ILoginAuditRecorder
{
    Task RecordSuccessAsync(Guid userId, string email, PortalAudience? audience, CancellationToken ct = default);
    Task RecordFailureAsync(string email, string reason, Guid? userId, CancellationToken ct = default);
    Task RecordLogoutAsync(Guid userId, string email, PortalAudience? audience, CancellationToken ct = default);
}
