using Workshop.Application.Common.Notifications;

namespace Workshop.Application.Tests;

internal class FakeNotificationDispatcher : INotificationDispatcher
{
    public List<NotificationRequest> Sent { get; } = new();

    public Task DispatchAsync(NotificationRequest request, CancellationToken ct = default)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }
}

internal class FakeCaseNotificationRecipients : ICaseNotificationRecipients
{
    public IReadOnlyList<NotificationRecipient> Next { get; set; } = Array.Empty<NotificationRecipient>();

    public Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        Guid? insuranceCaseId, Guid? retailCaseId, CaseAudienceFlags audiences, CancellationToken ct = default)
        => Task.FromResult(Next);
}
