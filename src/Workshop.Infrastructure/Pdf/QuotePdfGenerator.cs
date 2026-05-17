using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Files;
using Workshop.Application.Common.Pdf;
using Workshop.Domain.Enums;

namespace Workshop.Infrastructure.Pdf;

/// <summary>
/// Renders the Paint Bull quote PDF using QuestPDF. The layout follows the
/// printable form shown in εφαρμογη web.docx (images 5-6): header with company
/// info, customer + vehicle block, work items table, parts table, totals block
/// (labor + parts + VAT + customer participation).
/// </summary>
public class QuotePdfGenerator(IWorkshopDbContext db, IFileStore files) : IQuotePdfGenerator
{
    static QuotePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateAsync(Guid quoteId, CancellationToken ct = default)
    {
        var data = await LoadAsync(quoteId, ct)
            ?? throw new KeyNotFoundException($"Quote {quoteId} not found");

        var doc = new QuoteDocument(data);
        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        ms.Position = 0;

        var folder = $"quotes/case-{data.InsuranceCaseId:N}";
        var fileName = $"{data.QuoteNumber}.pdf";
        var stored = await files.SaveAsync(folder, fileName, ms, "application/pdf", ct);
        return stored.RelativePath;
    }

    private async Task<QuoteData?> LoadAsync(Guid quoteId, CancellationToken ct)
    {
        var profile = await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(ct);

        var q = await db.Quotes.AsNoTracking()
            .Where(x => x.Id == quoteId)
            .Select(x => new
            {
                x.Id, x.QuoteNumber, x.IssueDate,
                x.InsuranceCaseId,
                x.LaborSubtotal, x.PartsSubtotal,
                x.LaborDiscountAmount, x.PartsDiscountAmount,
                x.Subtotal, x.VatRate, x.VatAmount, x.Total,
                x.CustomerParticipation, x.Notes
            })
            .FirstOrDefaultAsync(ct);
        if (q is null) return null;

        var caseRow = await db.InsuranceCases.AsNoTracking()
            .Where(c => c.Id == q.InsuranceCaseId)
            .Select(c => new
            {
                c.CaseNumber, c.ClaimNumber, c.AccidentDate,
                CustomerType = c.Customer.CustomerType,
                CustomerName = c.Customer.CustomerType == CustomerType.Company
                    ? (c.Customer.CompanyName ?? "")
                    : (((c.Customer.LastName ?? "") + " " + (c.Customer.FirstName ?? "")).Trim()),
                CustomerVat = c.Customer.VatNumber,
                CustomerPhone = c.Customer.MobilePhone,
                CustomerAddress = c.Customer.AddressLine,
                VehiclePlate = c.Vehicle.PlateNumber,
                VehicleBrand = c.Vehicle.Brand,
                VehicleModel = c.Vehicle.Model,
                VehicleYear = c.Vehicle.Year,
                InsurerName = c.InsuranceCompany.Name
            })
            .FirstOrDefaultAsync(ct);

        var workItems = await db.WorkItems.AsNoTracking()
            .Where(w => w.Assessment.InsuranceCaseId == q.InsuranceCaseId)
            .OrderBy(w => w.CreatedAt)
            .Select(w => new WorkItemRow(
                w.BodyPanel != null ? w.BodyPanel.Code : "",
                w.Description,
                w.Total))
            .ToListAsync(ct);

        var parts = await db.InsurancePartLines.AsNoTracking()
            .Where(p => p.Assessment.InsuranceCaseId == q.InsuranceCaseId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PartLineRow(
                p.PartName,
                p.PartType,
                p.Quantity,
                p.UnitCost,
                p.Total))
            .ToListAsync(ct);

        return new QuoteData(
            CompanyName: profile?.Name ?? "Paint Bull",
            CompanyAddress: profile?.AddressLine ?? "",
            CompanyCity: profile?.City ?? "",
            CompanyPhone: profile?.Phone ?? "",
            CompanyVat: profile?.VatNumber ?? "",
            QuoteNumber: q.QuoteNumber,
            IssueDate: q.IssueDate,
            InsuranceCaseId: q.InsuranceCaseId,
            CaseNumber: caseRow?.CaseNumber ?? "",
            ClaimNumber: caseRow?.ClaimNumber,
            AccidentDate: caseRow?.AccidentDate,
            CustomerName: caseRow?.CustomerName ?? "",
            CustomerVat: caseRow?.CustomerVat,
            CustomerPhone: caseRow?.CustomerPhone ?? "",
            CustomerAddress: caseRow?.CustomerAddress,
            VehiclePlate: caseRow?.VehiclePlate ?? "",
            VehicleBrandModel: $"{caseRow?.VehicleBrand} {caseRow?.VehicleModel}".Trim(),
            VehicleYear: caseRow?.VehicleYear,
            InsurerName: caseRow?.InsurerName ?? "",
            WorkItems: workItems,
            Parts: parts,
            LaborSubtotal: q.LaborSubtotal,
            PartsSubtotal: q.PartsSubtotal,
            LaborDiscount: q.LaborDiscountAmount,
            PartsDiscount: q.PartsDiscountAmount,
            Subtotal: q.Subtotal,
            VatRate: q.VatRate,
            VatAmount: q.VatAmount,
            Total: q.Total,
            CustomerParticipation: q.CustomerParticipation,
            Notes: q.Notes);
    }
}

internal record WorkItemRow(string PanelCode, string Description, decimal Total);
internal record PartLineRow(string PartName, PartType PartType, decimal Quantity, decimal UnitCost, decimal Total);

internal record QuoteData(
    string CompanyName, string CompanyAddress, string CompanyCity, string CompanyPhone, string CompanyVat,
    string QuoteNumber, DateOnly IssueDate, Guid InsuranceCaseId, string CaseNumber, string? ClaimNumber,
    DateOnly? AccidentDate,
    string CustomerName, string? CustomerVat, string CustomerPhone, string? CustomerAddress,
    string VehiclePlate, string VehicleBrandModel, int? VehicleYear, string InsurerName,
    IReadOnlyList<WorkItemRow> WorkItems, IReadOnlyList<PartLineRow> Parts,
    decimal LaborSubtotal, decimal PartsSubtotal,
    decimal? LaborDiscount, decimal? PartsDiscount,
    decimal Subtotal, decimal VatRate, decimal VatAmount, decimal Total,
    decimal? CustomerParticipation, string? Notes);
