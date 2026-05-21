using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Messaging;
using Workshop.Domain.Enums;

namespace Workshop.Application.Features.Dashboard;

public record CalendarRepairRow(
    Guid CaseId,
    bool IsRetail,
    string CaseNumber,
    string Plate,
    string VehicleBrandModel,
    string CustomerLabel,
    string? TechnicianName,
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    RepairStatus Status);

public record ListCalendarRepairsQuery(DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<CalendarRepairRow>>;

public class ListCalendarRepairsHandler(IWorkshopDbContext db)
    : IRequestHandler<ListCalendarRepairsQuery, IReadOnlyList<CalendarRepairRow>>
{
    public async Task<IReadOnlyList<CalendarRepairRow>> Handle(ListCalendarRepairsQuery q, CancellationToken ct)
    {
        var fromDate = q.From <= q.To ? q.From : q.To;
        var toDate = q.From <= q.To ? q.To : q.From;

        // Explicit joins (no required-nav projection) to stay safe under EF InMemory tests.
        var insurance = await (
            from r in db.Repairs.AsNoTracking()
            join c in db.InsuranceCases.AsNoTracking() on r.InsuranceCaseId equals c.Id
            join v in db.Vehicles.AsNoTracking() on c.VehicleId equals v.Id
            join cu in db.Customers.AsNoTracking() on c.CustomerId equals cu.Id
            where r.ScheduledDate >= fromDate && r.ScheduledDate <= toDate
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
                r.ScheduledDate,
                r.ScheduledTime,
                r.Status
            }).ToListAsync(ct);

        var retail = await (
            from r in db.RetailRepairs.AsNoTracking()
            join c in db.RetailCases.AsNoTracking() on r.RetailCaseId equals c.Id
            join v in db.Vehicles.AsNoTracking() on c.VehicleId equals v.Id
            join cu in db.Customers.AsNoTracking() on c.CustomerId equals cu.Id
            where r.ScheduledDate >= fromDate && r.ScheduledDate <= toDate
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
                r.ScheduledDate,
                r.ScheduledTime,
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

        var rows = insurance.Select(x => new CalendarRepairRow(
                x.CaseId, x.IsRetail, x.CaseNumber, x.Plate,
                $"{x.Brand} {x.Model}".Trim(),
                CustomerLabel(x.FirstName, x.LastName, x.CompanyName),
                x.TechnicianId.HasValue ? techNames.GetValueOrDefault(x.TechnicianId.Value) : null,
                x.ScheduledDate,
                x.ScheduledTime,
                x.Status))
            .Concat(retail.Select(x => new CalendarRepairRow(
                x.CaseId, x.IsRetail, x.CaseNumber, x.Plate,
                $"{x.Brand} {x.Model}".Trim(),
                CustomerLabel(x.FirstName, x.LastName, x.CompanyName),
                null,
                x.ScheduledDate,
                x.ScheduledTime,
                x.Status)))
            .OrderBy(r => r.ScheduledDate)
            .ThenBy(r => r.ScheduledTime ?? TimeOnly.MinValue)
            .ThenBy(r => r.CaseNumber)
            .ToList();

        return rows;
    }
}
