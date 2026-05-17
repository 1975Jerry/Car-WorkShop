using Workshop.Domain.Common;
using Workshop.Domain.Enums;

namespace Workshop.Domain.Entities.Shared;

public class BodyPanel : Entity
{
    public string Code { get; set; } = string.Empty;
    public string DescriptionGr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public BodyPanelCategory Category { get; set; }
    public PanelSide Side { get; set; }
    public decimal? DiagramX { get; set; }
    public decimal? DiagramY { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<BodyPanelOperation> AllowedOperations { get; set; } = new List<BodyPanelOperation>();
}

public class BodyPanelOperation
{
    public Guid BodyPanelId { get; set; }
    public BodyPanel BodyPanel { get; set; } = null!;
    public OperationType Operation { get; set; }
}

public class ServiceCatalog : Entity
{
    public string Code { get; set; } = string.Empty;
    public string NameGr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public decimal? DefaultPrice { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
