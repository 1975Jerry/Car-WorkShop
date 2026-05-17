using MediatR;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Dashboard;

public record GetDashboardKpisQuery : IRequest<DashboardKpisDto>;

public class GetDashboardKpisHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    public async Task<DashboardKpisDto> Handle(GetDashboardKpisQuery _, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var openInsurance = await db.InsuranceCases.AsNoTracking()
            .CountAsync(c => c.Status != InsuranceCaseStatus.CaseClosed, ct);
        var openRetail = await db.RetailCases.AsNoTracking()
            .CountAsync(c => c.Status != RetailCaseStatus.Closed, ct);

        // Parts pending: any non-terminal received status across both insurance and retail.
        var pendingInsurance = await db.InsurancePartLines.AsNoTracking()
            .CountAsync(p =>
                p.ReceivedStatus == PartReceivedStatus.Pending ||
                p.ReceivedStatus == PartReceivedStatus.Ordered ||
                p.ReceivedStatus == PartReceivedStatus.InTransit, ct);
        var pendingRetail = await db.RetailPartLines.AsNoTracking()
            .CountAsync(p =>
                p.ReceivedStatus == PartReceivedStatus.Pending ||
                p.ReceivedStatus == PartReceivedStatus.Ordered ||
                p.ReceivedStatus == PartReceivedStatus.InTransit, ct);

        var repairsToday = await db.Repairs.AsNoTracking()
            .CountAsync(r => r.ScheduledDate == today, ct);
        var retailRepairsToday = await db.RetailRepairs.AsNoTracking()
            .CountAsync(r => r.ScheduledDate == today, ct);

        var repairsInProgress = await db.Repairs.AsNoTracking()
            .CountAsync(r => r.Status == RepairStatus.InProgress, ct);
        var retailInProgress = await db.RetailRepairs.AsNoTracking()
            .CountAsync(r => r.Status == RepairStatus.InProgress, ct);

        // Settlement pipeline: approved-but-not-yet-paid for insurance,
        // plus open retail totals (status not Closed).
        var insurancePipeline = await (
            from c in db.InsuranceCases.AsNoTracking()
            join a in db.InsuranceApprovals.AsNoTracking() on c.Id equals a.InsuranceCaseId
            where c.Status != InsuranceCaseStatus.CaseClosed
                  && a.ApprovalStatus == ApprovalStatus.Approved
            select (decimal?)a.ApprovedAmount).SumAsync(ct);

        var paidByCase = await db.Payments.AsNoTracking()
            .Where(p => p.InsuranceCaseId != null)
            .GroupBy(p => p.InsuranceCaseId)
            .Select(g => new { CaseId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var totalInsurancePaid = paidByCase.Sum(x => x.Paid);
        var insuranceRemaining = (insurancePipeline ?? 0m) - totalInsurancePaid;
        if (insuranceRemaining < 0) insuranceRemaining = 0;

        var retailPipeline = await db.RetailCases.AsNoTracking()
            .Where(c => c.Status != RetailCaseStatus.Closed && c.Status != RetailCaseStatus.Paid)
            .SumAsync(c => (decimal?)c.TotalWithVat, ct) ?? 0m;

        // Avg cycle time for cases closed in the trailing 30 days.
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var cycleSince = nowUtc.AddDays(-30);
        var insClosedDates = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.ClosedAt != null && c.ClosedAt >= cycleSince)
            .Select(c => new { c.CreatedAt, ClosedAt = c.ClosedAt!.Value })
            .ToListAsync(ct);
        var retClosedDates = await db.RetailCases.AsNoTracking()
            .Where(c => c.CompletedAt != null && c.CompletedAt >= cycleSince)
            .Select(c => new { c.CreatedAt, ClosedAt = c.CompletedAt!.Value })
            .ToListAsync(ct);
        var cycleDays = insClosedDates.Concat(retClosedDates)
            .Select(x => (x.ClosedAt - x.CreatedAt).TotalDays)
            .ToList();
        double? avgCycle = cycleDays.Count == 0 ? null : cycleDays.Average();

        return new DashboardKpisDto(
            openInsurance,
            openRetail,
            pendingInsurance + pendingRetail,
            repairsToday + retailRepairsToday,
            repairsInProgress + retailInProgress,
            insuranceRemaining + retailPipeline,
            avgCycle);
    }
}

public record GetThroughputTrendQuery(int Weeks = 12) : IRequest<IReadOnlyList<ThroughputWeekRow>>;

public class GetThroughputTrendHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<GetThroughputTrendQuery, IReadOnlyList<ThroughputWeekRow>>
{
    public async Task<IReadOnlyList<ThroughputWeekRow>> Handle(GetThroughputTrendQuery q, CancellationToken ct)
    {
        var weeks = Math.Clamp(q.Weeks, 1, 52);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        // Anchor weeks at Monday so the chart aligns with the Greek work week.
        var thisWeekStart = today.AddDays(-((int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1));
        var firstWeekStart = thisWeekStart.AddDays(-7 * (weeks - 1));
        var rangeStartUtc = firstWeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var insOpened = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.CreatedAt >= rangeStartUtc)
            .Select(c => c.CreatedAt).ToListAsync(ct);
        var retOpened = await db.RetailCases.AsNoTracking()
            .Where(c => c.CreatedAt >= rangeStartUtc)
            .Select(c => c.CreatedAt).ToListAsync(ct);
        var insClosed = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.ClosedAt != null && c.ClosedAt >= rangeStartUtc)
            .Select(c => c.ClosedAt!.Value).ToListAsync(ct);
        var retClosed = await db.RetailCases.AsNoTracking()
            .Where(c => c.CompletedAt != null && c.CompletedAt >= rangeStartUtc)
            .Select(c => c.CompletedAt!.Value).ToListAsync(ct);

        var rows = new ThroughputWeekRow[weeks];
        for (var i = 0; i < weeks; i++)
        {
            rows[i] = new ThroughputWeekRow(firstWeekStart.AddDays(7 * i), 0, 0);
        }

        void Bump(DateTime ts, bool isOpen)
        {
            var d = DateOnly.FromDateTime(ts);
            var idx = (d.DayNumber - firstWeekStart.DayNumber) / 7;
            if (idx < 0 || idx >= weeks) return;
            var row = rows[idx];
            rows[idx] = isOpen
                ? row with { Opened = row.Opened + 1 }
                : row with { Closed = row.Closed + 1 };
        }

        foreach (var ts in insOpened) Bump(ts, isOpen: true);
        foreach (var ts in retOpened) Bump(ts, isOpen: true);
        foreach (var ts in insClosed) Bump(ts, isOpen: false);
        foreach (var ts in retClosed) Bump(ts, isOpen: false);

        return rows;
    }
}

public record GetRevenuePeriodQuery(int PeriodDays = 30) : IRequest<RevenuePeriodDto>;

public class GetRevenuePeriodHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<GetRevenuePeriodQuery, RevenuePeriodDto>
{
    public async Task<RevenuePeriodDto> Handle(GetRevenuePeriodQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.PeriodDays, 1, 365);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var currStart = today.AddDays(-days + 1);
        var prevStart = currStart.AddDays(-days);
        var prevEnd = currStart.AddDays(-1);

        var current = await db.Payments.AsNoTracking()
            .Where(p => p.PaymentDate >= currStart && p.PaymentDate <= today)
            .GroupBy(_ => 1)
            .Select(g => new { Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var previous = await db.Payments.AsNoTracking()
            .Where(p => p.PaymentDate >= prevStart && p.PaymentDate <= prevEnd)
            .GroupBy(_ => 1)
            .Select(g => new { Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        return new RevenuePeriodDto(
            current?.Amount ?? 0m,
            previous?.Amount ?? 0m,
            current?.Count ?? 0,
            previous?.Count ?? 0,
            days);
    }
}

public record GetTodayRepairsQuery : IRequest<IReadOnlyList<TodayRepairRow>>;

public class GetTodayRepairsHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<GetTodayRepairsQuery, IReadOnlyList<TodayRepairRow>>
{
    public async Task<IReadOnlyList<TodayRepairRow>> Handle(GetTodayRepairsQuery _, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Explicit joins (no required-nav projection) to stay safe under EF InMemory tests.
        var insurance = await (
            from r in db.Repairs.AsNoTracking()
            join c in db.InsuranceCases.AsNoTracking() on r.InsuranceCaseId equals c.Id
            join v in db.Vehicles.AsNoTracking() on c.VehicleId equals v.Id
            join cu in db.Customers.AsNoTracking() on c.CustomerId equals cu.Id
            where r.ScheduledDate == today
            select new
            {
                CaseId = c.Id,
                IsRetail = false,
                c.CaseNumber,
                Plate = v.PlateNumber,
                Brand = v.Brand,
                Model = v.Model,
                cu.FirstName,
                cu.LastName,
                cu.CompanyName,
                TechnicianId = r.TechnicianId,
                r.ScheduledTime,
                r.Status
            }).ToListAsync(ct);

        var retail = await (
            from r in db.RetailRepairs.AsNoTracking()
            join c in db.RetailCases.AsNoTracking() on r.RetailCaseId equals c.Id
            join v in db.Vehicles.AsNoTracking() on c.VehicleId equals v.Id
            join cu in db.Customers.AsNoTracking() on c.CustomerId equals cu.Id
            where r.ScheduledDate == today
            select new
            {
                CaseId = c.Id,
                IsRetail = true,
                c.CaseNumber,
                Plate = v.PlateNumber,
                Brand = v.Brand,
                Model = v.Model,
                cu.FirstName,
                cu.LastName,
                cu.CompanyName,
                TechnicianId = (Guid?)null,
                ScheduledTime = (TimeOnly?)null,
                r.Status
            }).ToListAsync(ct);

        var technicianIds = insurance.Where(x => x.TechnicianId.HasValue)
            .Select(x => x.TechnicianId!.Value).Distinct().ToList();
        var techNames = technicianIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => technicianIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        static string CustomerLabel(string? first, string? last, string? company)
        {
            if (!string.IsNullOrWhiteSpace(company)) return company!;
            var name = string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(name) ? "—" : name;
        }

        var rows = insurance.Select(x => new TodayRepairRow(
                x.CaseId, x.IsRetail, x.CaseNumber, x.Plate,
                $"{x.Brand} {x.Model}".Trim(),
                CustomerLabel(x.FirstName, x.LastName, x.CompanyName),
                x.TechnicianId.HasValue ? techNames.GetValueOrDefault(x.TechnicianId.Value) : null,
                x.ScheduledTime,
                x.Status))
            .Concat(retail.Select(x => new TodayRepairRow(
                x.CaseId, x.IsRetail, x.CaseNumber, x.Plate,
                $"{x.Brand} {x.Model}".Trim(),
                CustomerLabel(x.FirstName, x.LastName, x.CompanyName),
                null,
                x.ScheduledTime,
                x.Status)))
            .OrderBy(r => r.ScheduledTime ?? TimeOnly.MinValue)
            .ThenBy(r => r.CaseNumber)
            .ToList();

        return rows;
    }
}

public record GetBranchBreakdownQuery : IRequest<IReadOnlyList<BranchBreakdownRow>>;

public class GetBranchBreakdownHandler(IWorkshopDbContext db)
    : IRequestHandler<GetBranchBreakdownQuery, IReadOnlyList<BranchBreakdownRow>>
{
    public async Task<IReadOnlyList<BranchBreakdownRow>> Handle(GetBranchBreakdownQuery _, CancellationToken ct)
    {
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name })
            .ToListAsync(ct);

        var insurance = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Status != InsuranceCaseStatus.CaseClosed)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var retail = await db.RetailCases.AsNoTracking()
            .Where(c => c.Status != RetailCaseStatus.Closed)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Repairs in progress join: pull case BranchId via FK.
        var repairs = await (
            from r in db.Repairs.AsNoTracking()
            join c in db.InsuranceCases.AsNoTracking() on r.InsuranceCaseId equals c.Id
            where r.Status == RepairStatus.InProgress
            group r by c.BranchId into g
            select new { BranchId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var retailRepairs = await (
            from r in db.RetailRepairs.AsNoTracking()
            join c in db.RetailCases.AsNoTracking() on r.RetailCaseId equals c.Id
            where r.Status == RepairStatus.InProgress
            group r by c.BranchId into g
            select new { BranchId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var insuranceByBranch = insurance.ToDictionary(x => x.BranchId, x => x.Count);
        var retailByBranch = retail.ToDictionary(x => x.BranchId, x => x.Count);
        var repairsByBranch = repairs.ToDictionary(x => x.BranchId, x => x.Count);
        var retailRepairsByBranch = retailRepairs.ToDictionary(x => x.BranchId, x => x.Count);

        return branches.Select(b => new BranchBreakdownRow(
            b.Id, b.Name,
            insuranceByBranch.GetValueOrDefault(b.Id),
            retailByBranch.GetValueOrDefault(b.Id),
            repairsByBranch.GetValueOrDefault(b.Id) + retailRepairsByBranch.GetValueOrDefault(b.Id))).ToList();
    }
}

public record GetAgingBucketsQuery : IRequest<IReadOnlyList<AgingBucketRow>>;

public class GetAgingBucketsHandler(IWorkshopDbContext db, TimeProvider clock)
    : IRequestHandler<GetAgingBucketsQuery, IReadOnlyList<AgingBucketRow>>
{
    public async Task<IReadOnlyList<AgingBucketRow>> Handle(GetAgingBucketsQuery _, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var insurance = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Status != InsuranceCaseStatus.CaseClosed)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        var retail = await db.RetailCases.AsNoTracking()
            .Where(c => c.Status != RetailCaseStatus.Closed)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        int insBucket0 = 0, insBucket1 = 0, insBucket2 = 0, insBucket3 = 0;
        foreach (var dt in insurance)
        {
            var age = (int)Math.Floor((now - dt).TotalDays);
            if (age <= 7) insBucket0++;
            else if (age <= 30) insBucket1++;
            else if (age <= 60) insBucket2++;
            else insBucket3++;
        }
        int retBucket0 = 0, retBucket1 = 0, retBucket2 = 0, retBucket3 = 0;
        foreach (var dt in retail)
        {
            var age = (int)Math.Floor((now - dt).TotalDays);
            if (age <= 7) retBucket0++;
            else if (age <= 30) retBucket1++;
            else if (age <= 60) retBucket2++;
            else retBucket3++;
        }

        return new List<AgingBucketRow>
        {
            new("0-7", insBucket0, retBucket0),
            new("8-30", insBucket1, retBucket1),
            new("31-60", insBucket2, retBucket2),
            new("60+", insBucket3, retBucket3),
        };
    }
}

public record GetPartsVarianceQuery(int Take = 25) : IRequest<IReadOnlyList<PartsVarianceRow>>;

public class GetPartsVarianceHandler(IWorkshopDbContext db)
    : IRequestHandler<GetPartsVarianceQuery, IReadOnlyList<PartsVarianceRow>>
{
    public async Task<IReadOnlyList<PartsVarianceRow>> Handle(GetPartsVarianceQuery q, CancellationToken ct)
    {
        // Per-case approved amount and parts subtotal — flag cases where parts exceed approval.
        var approved = await db.InsuranceApprovals.AsNoTracking()
            .Where(a => a.ApprovalStatus == ApprovalStatus.Approved)
            .Select(a => new { a.InsuranceCaseId, a.ApprovedAmount })
            .ToListAsync(ct);

        if (approved.Count == 0) return [];

        var caseIds = approved.Select(a => a.InsuranceCaseId).ToList();

        var partsByCase = await db.InsurancePartLines.AsNoTracking()
            .Where(p => caseIds.Contains(p.Assessment.InsuranceCaseId)
                        && p.ReceivedStatus != PartReceivedStatus.Cancelled)
            .GroupBy(p => p.Assessment.InsuranceCaseId)
            .Select(g => new { CaseId = g.Key, PartsCost = g.Sum(p => p.Total) })
            .ToDictionaryAsync(x => x.CaseId, x => x.PartsCost, ct);

        var caseNumbers = await db.InsuranceCases.AsNoTracking()
            .Where(c => caseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CaseNumber, ct);

        return approved
            .Select(a =>
            {
                var partsCost = partsByCase.GetValueOrDefault(a.InsuranceCaseId, 0m);
                return new PartsVarianceRow(
                    a.InsuranceCaseId,
                    caseNumbers.GetValueOrDefault(a.InsuranceCaseId, "—"),
                    a.ApprovedAmount,
                    partsCost,
                    partsCost - a.ApprovedAmount);
            })
            .OrderByDescending(r => r.Variance)
            .Take(q.Take)
            .ToList();
    }
}
