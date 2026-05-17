namespace Workshop.Domain.Enums;

public enum OperationType
{
    Polish = 1,
    PDR = 2,
    RemoveRefit = 3,
    Replace = 4,
    DisassembleAssemble = 5,
    Repair = 6,
    Paint = 7,
    RepairPaint = 8,
    Weld = 9,
    Other = 10
}

public enum PartType
{
    Original = 1,
    NonOEM = 2,
    MTX = 3,
    Other = 4
}

public enum InsuranceCaseStatus
{
    NewCase = 1,
    AssessorAppointment = 2,
    Assessment = 3,
    InsuranceApproval = 4,
    CustomerAssignment = 5,
    PartsApprovalAndOrder = 6,
    RepairScheduling = 7,
    RepairInProgress = 8,
    RepairCompleted = 9,
    Settlement = 10,
    PaymentConfirmed = 11,
    CaseClosed = 12
}

public enum RetailCaseStatus
{
    Quoted = 1,
    Accepted = 2,
    InProgress = 3,
    Completed = 4,
    Paid = 5,
    Closed = 6
}

public enum PartReceivedStatus
{
    Pending = 1,
    Ordered = 2,
    InTransit = 3,
    Received = 4,
    Defective = 5,
    Cancelled = 6
}

public enum AvailabilityStatus
{
    Available = 1,
    OutOfStock = 2,
    Discontinued = 3,
    Unknown = 4
}

public enum DocumentType
{
    CaseForm = 1,
    InsuranceForm = 2,
    Invoice = 3,
    Receipt = 4,
    Quote = 5,
    IdCopy = 6,
    VehicleLicense = 7,
    Other = 99
}

public enum CustomerType
{
    Individual = 1,
    Company = 2
}

public enum PortalAudience
{
    Staff = 1,
    Customer = 2,
    Insurance = 3,
    Supplier = 4
}

public enum FuelType
{
    Petrol = 1,
    Diesel = 2,
    LPG = 3,
    Electric = 4,
    Hybrid = 5,
    Other = 99
}

public enum CaseTriggerEvent
{
    BookAssessorAppointment,
    CompleteAssessment,
    SubmitForInsuranceApproval,
    ApprovalReceived,
    ApprovalRejected,
    CustomerAccepts,
    AllPartsReceived,
    StartRepair,
    CompleteRepair,
    IssueSettlement,
    ConfirmPayment,
    CloseCase,
    Cancel
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    PartialApproval = 3,
    Rejected = 4
}

public enum RepairStatus
{
    Scheduled = 1,
    InProgress = 2,
    OnHold = 3,
    Completed = 4
}

public enum PhotoPhase
{
    Intake = 1,
    Damage = 2,
    Intermediate = 3,
    Completion = 4
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    InsurancePayout = 4,
    Other = 99
}

public enum PanelSide
{
    Center = 0,
    Left = 1,
    Right = 2
}

public enum BodyPanelCategory
{
    External = 1,
    Internal = 2,
    Safety = 3
}

public enum CasePriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3
}
