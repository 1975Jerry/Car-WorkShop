using Workshop.Domain.Common;
using Workshop.Domain.Entities.Identity;
using Workshop.Domain.Entities.Insurance;
using Workshop.Domain.Entities.Retail;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.CrossCutting;

public class CaseEvent : Entity
{
    public Guid? InsuranceCaseId { get; set; }
    public InsuranceCase? InsuranceCase { get; set; }
    public Guid? RetailCaseId { get; set; }
    public RetailCase? RetailCase { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid TriggeredById { get; set; }
    public User TriggeredBy { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class AuditLog : Entity
{
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? Changes { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
}

public class Notification : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime OccurredAt { get; set; }
}
