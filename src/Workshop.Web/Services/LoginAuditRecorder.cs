using Microsoft.Extensions.DependencyInjection;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Entities.CrossCutting;
using Workshop.Domain.Enums;
using Workshop.Infrastructure.Persistence;

namespace Workshop.Web.Services;

public class LoginAuditRecorder : ILoginAuditRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<LoginAuditRecorder> _log;

    public LoginAuditRecorder(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor http,
        ILogger<LoginAuditRecorder> log)
    {
        _scopeFactory = scopeFactory;
        _http = http;
        _log = log;
    }

    public Task RecordSuccessAsync(Guid userId, string email, PortalAudience? audience, CancellationToken ct = default)
        => WriteAsync(new LoginAuditEntry
        {
            UserId = userId,
            Email = email,
            Event = LoginAuditEvent.Login,
            PortalAudience = audience,
        }, ct);

    public Task RecordFailureAsync(string email, string reason, Guid? userId, CancellationToken ct = default)
        => WriteAsync(new LoginAuditEntry
        {
            UserId = userId,
            Email = email,
            Event = LoginAuditEvent.LoginFailed,
            FailureReason = reason,
        }, ct);

    public Task RecordLogoutAsync(Guid userId, string email, PortalAudience? audience, CancellationToken ct = default)
        => WriteAsync(new LoginAuditEntry
        {
            UserId = userId,
            Email = email,
            Event = LoginAuditEvent.Logout,
            PortalAudience = audience,
        }, ct);

    private async Task WriteAsync(LoginAuditEntry entry, CancellationToken ct)
    {
        try
        {
            var http = _http.HttpContext;
            entry.OccurredAt = DateTime.UtcNow;
            entry.IpAddress = http?.Connection.RemoteIpAddress?.ToString();
            entry.UserAgent = Truncate(http?.Request.Headers.UserAgent.ToString(), 512);
            entry.Email = Truncate(entry.Email, 256) ?? string.Empty;
            entry.FailureReason = Truncate(entry.FailureReason, 256);

            // Fresh DI scope so the audit write doesn't share the Blazor
            // circuit's serialized DbContext and won't roll back with an
            // unrelated failure on the same scope.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
            db.LoginAuditEntries.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Auditing must never block the auth flow. Log + swallow.
            _log.LogError(ex, "Failed to record login audit entry for {Email}", entry.Email);
        }
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
