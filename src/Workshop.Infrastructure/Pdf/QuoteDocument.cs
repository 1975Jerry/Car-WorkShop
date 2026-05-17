using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Workshop.Domain.Enums;

namespace Workshop.Infrastructure.Pdf;

internal class QuoteDocument(QuoteData data) : IDocument
{
    public DocumentMetadata GetMetadata()
    {
        var meta = DocumentMetadata.Default;
        meta.Title = $"Quote {data.QuoteNumber}";
        meta.Author = data.CompanyName;
        return meta;
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span($"{data.CompanyName} · {data.CompanyPhone} · ΑΦΜ {data.CompanyVat}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeHeader(IContainer c)
    {
        c.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(data.CompanyName).Bold().FontSize(16);
                col.Item().Text(data.CompanyAddress).FontSize(9);
                col.Item().Text($"{data.CompanyCity} · {data.CompanyPhone}").FontSize(9);
                col.Item().Text($"ΑΦΜ {data.CompanyVat}").FontSize(9);
            });
            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().Text("ΠΡΟΣΦΟΡΑ").Bold().FontSize(18);
                col.Item().Text($"Αρ. {data.QuoteNumber}").FontSize(10);
                col.Item().Text($"Ημ/νία: {data.IssueDate:dd/MM/yyyy}").FontSize(9);
                col.Item().Text($"Φάκελος: {data.CaseNumber}").FontSize(9);
            });
        });
    }

    private void ComposeContent(IContainer c)
    {
        c.PaddingVertical(10).Column(col =>
        {
            col.Spacing(8);
            col.Item().Element(ComposeCustomerVehicleBlock);
            if (data.WorkItems.Count > 0)
                col.Item().Element(ComposeWorkItemsTable);
            if (data.Parts.Count > 0)
                col.Item().Element(ComposePartsTable);
            col.Item().Element(ComposeTotalsBlock);
            if (!string.IsNullOrWhiteSpace(data.Notes))
                col.Item().Element(ComposeNotes);
        });
    }

    private void ComposeCustomerVehicleBlock(IContainer c)
    {
        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Πελάτης").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(data.CustomerName).Bold();
                if (!string.IsNullOrWhiteSpace(data.CustomerVat))
                    col.Item().Text($"ΑΦΜ {data.CustomerVat}");
                col.Item().Text(data.CustomerPhone);
                if (!string.IsNullOrWhiteSpace(data.CustomerAddress))
                    col.Item().Text(data.CustomerAddress!);
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Όχημα").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(data.VehicleBrandModel).Bold();
                col.Item().Text($"Πινακίδα: {data.VehiclePlate}"
                    + (data.VehicleYear.HasValue ? $" · Έτος {data.VehicleYear.Value}" : ""));
                col.Item().Text($"Ασφαλιστική: {data.InsurerName}");
                if (!string.IsNullOrWhiteSpace(data.ClaimNumber))
                    col.Item().Text($"Αρ. Ζημιάς: {data.ClaimNumber}");
                if (data.AccidentDate.HasValue)
                    col.Item().Text($"Ημ/νία Ατυχήματος: {data.AccidentDate.Value:dd/MM/yyyy}");
            });
        });
    }

    private void ComposeWorkItemsTable(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Text("Εργασίες").Bold().FontSize(11);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(50);   // Panel code
                    cd.RelativeColumn();     // Description
                    cd.ConstantColumn(80);   // Total
                });
                t.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("Α/Α");
                    h.Cell().Element(HeaderCell).Text("Περιγραφή");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Σύνολο");
                });
                foreach (var w in data.WorkItems)
                {
                    t.Cell().Element(BodyCell).Text(w.PanelCode);
                    t.Cell().Element(BodyCell).Text(w.Description);
                    t.Cell().Element(BodyCell).AlignRight().Text(w.Total.ToString("N2"));
                }
                t.Cell().ColumnSpan(2).Element(FootCell).AlignRight().Text("Σύνολο Εργασίας").Bold();
                t.Cell().Element(FootCell).AlignRight().Text(data.LaborSubtotal.ToString("N2")).Bold();
            });
        });
    }

    private void ComposePartsTable(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Text("Ανταλλακτικά").Bold().FontSize(11);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3);    // Name
                    cd.ConstantColumn(70);   // Type
                    cd.ConstantColumn(50);   // Qty
                    cd.ConstantColumn(70);   // Unit
                    cd.ConstantColumn(80);   // Total
                });
                t.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("Περιγραφή");
                    h.Cell().Element(HeaderCell).Text("Τύπος");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Ποσότ.");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Τιμή");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Σύνολο");
                });
                foreach (var p in data.Parts)
                {
                    t.Cell().Element(BodyCell).Text(p.PartName);
                    t.Cell().Element(BodyCell).Text(PartTypeLabel(p.PartType));
                    t.Cell().Element(BodyCell).AlignRight().Text(p.Quantity.ToString("0.##"));
                    t.Cell().Element(BodyCell).AlignRight().Text(p.UnitCost.ToString("N2"));
                    t.Cell().Element(BodyCell).AlignRight().Text(p.Total.ToString("N2"));
                }
                t.Cell().ColumnSpan(4).Element(FootCell).AlignRight().Text("Σύνολο Ανταλλακτικών").Bold();
                t.Cell().Element(FootCell).AlignRight().Text(data.PartsSubtotal.ToString("N2")).Bold();
            });
        });
    }

    private void ComposeTotalsBlock(IContainer c)
    {
        c.AlignRight().Column(col =>
        {
            col.Spacing(2);
            TotalsRow(col, "Σύνολο Εργασίας", data.LaborSubtotal);
            TotalsRow(col, "Σύνολο Ανταλλακτικών", data.PartsSubtotal);
            if (data.LaborDiscount.HasValue && data.LaborDiscount.Value > 0)
                TotalsRow(col, "Έκπτωση Εργασίας", -data.LaborDiscount.Value);
            if (data.PartsDiscount.HasValue && data.PartsDiscount.Value > 0)
                TotalsRow(col, "Έκπτωση Ανταλλακτικών", -data.PartsDiscount.Value);
            TotalsRow(col, "Μερικό Σύνολο", data.Subtotal);
            TotalsRow(col, $"ΦΠΑ {data.VatRate:0.##}%", data.VatAmount);
            col.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Black);
            TotalsRow(col, "ΣΥΝΟΛΟ", data.Total, bold: true);
            if (data.CustomerParticipation.HasValue && data.CustomerParticipation.Value > 0)
                TotalsRow(col, "Συμμετοχή Πελάτη", data.CustomerParticipation.Value, bold: true);
        });
    }

    private static void TotalsRow(ColumnDescriptor col, string label, decimal amount, bool bold = false)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(160).AlignRight().Text(text =>
            {
                var span = text.Span(label).FontSize(10);
                if (bold) span.Bold();
            });
            r.ConstantItem(80).AlignRight().Text(text =>
            {
                var span = text.Span(amount.ToString("N2")).FontSize(10);
                if (bold) span.Bold();
            });
        });
    }

    private void ComposeNotes(IContainer c) =>
        c.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
        {
            col.Item().Text("Σημειώσεις").Bold().FontSize(9);
            col.Item().Text(data.Notes!);
        });

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten4).PaddingVertical(4).PaddingHorizontal(6)
         .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static IContainer BodyCell(IContainer c) =>
        c.PaddingVertical(3).PaddingHorizontal(6).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten3);

    private static IContainer FootCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten5).PaddingVertical(4).PaddingHorizontal(6);

    private static string PartTypeLabel(PartType t) => t switch
    {
        PartType.Original => "Γνήσιο",
        PartType.NonOEM => "Μη γνήσιο",
        PartType.MTX => "Μεταχειρ.",
        _ => "Άλλο"
    };
}
