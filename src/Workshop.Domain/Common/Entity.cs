namespace Workshop.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}

public interface IBranchScoped
{
    Guid BranchId { get; }
}
