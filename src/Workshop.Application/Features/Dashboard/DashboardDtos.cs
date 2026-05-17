namespace Workshop.Application.Features.Dashboard;

public record DashboardKpisDto(
    int OpenInsuranceCases,
    int OpenRetailCases,
    int PendingParts,
    int RepairsScheduledToday,
    int RepairsInProgress,
    decimal SettlementPipelineValue,
    double? AvgCycleTimeDays);

public record ThroughputWeekRow(
    DateOnly WeekStart,
    int Opened,
    int Closed);

public record RevenuePeriodDto(
    decimal CurrentPeriodAmount,
    decimal PreviousPeriodAmount,
    int CurrentPeriodCount,
    int PreviousPeriodCount,
    int PeriodDays);

public record TodayRepairRow(
    Guid CaseId,
    bool IsRetail,
    string CaseNumber,
    string Plate,
    string VehicleBrandModel,
    string CustomerLabel,
    string? TechnicianName,
    TimeOnly? ScheduledTime,
    Workshop.Domain.Enums.RepairStatus Status);

public record BranchBreakdownRow(
    Guid BranchId,
    string BranchName,
    int InsuranceCases,
    int RetailCases,
    int RepairsInProgress);

public record AgingBucketRow(
    string BucketLabel,
    int InsuranceCases,
    int RetailCases);

public record PartsVarianceRow(
    Guid InsuranceCaseId,
    string CaseNumber,
    decimal ApprovedAmount,
    decimal PartsCost,
    decimal Variance);

public record TechnicianProductivityRow(
    Guid TechnicianId,
    string TechnicianName,
    int RepairsCompleted,
    int RepairsInProgress,
    double? AvgCompletionDays);
