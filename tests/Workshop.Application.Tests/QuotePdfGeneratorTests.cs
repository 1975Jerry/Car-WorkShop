using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.Quotes;
using Workshop.Domain.Entities.Shared;
using Workshop.Infrastructure.Pdf;

namespace Workshop.Application.Tests;

public class QuotePdfGeneratorTests
{
    [Fact]
    public async Task Generate_WritesNonEmptyPdfThroughFileStore()
    {
        await using var db = TestDb.NewContext();
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);

        await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(caseId, new AssessmentUpsertDto(
                DateOnly.FromDateTime(DateTime.Today), false, null, null, 300m,
                DateOnly.FromDateTime(DateTime.Today), false, null,
                new[] { new WorkItemUpsertDto(null, panelAllowed, "Front bumper",
                    100m, null, null, null, null, 200m, null, null, null, null, null) })),
            default);

        db.CompanyProfiles.Add(new CompanyProfile
        {
            Name = "Paint Bull", AddressLine = "Παν. Τσαλδάρη 41", City = "Ταύρος",
            Phone = "2102202898", VatNumber = "099999999", DefaultVatRate = 24m
        });
        await db.SaveChangesAsync();

        var files = new FakeFileStore();
        // Use the real PDF generator (QuestPDF) with the in-memory db + fake files.
        var pdf = new QuotePdfGenerator(db, files);
        var user = new TestCurrentUser();

        // Issue a quote via the real handler (which calls the generator).
        var quoteHandler = new IssueQuoteHandler(db, user, TimeProvider.System, pdf, new FakeNotificationDispatcher(), new FakeCaseNotificationRecipients());
        var quoteId = await quoteHandler.Handle(new IssueQuoteCommand(caseId), default);

        // Quote.PdfPath should be set to a non-empty path.
        var saved = await db.Quotes.AsNoTracking().FirstAsync(q => q.Id == quoteId);
        Assert.False(string.IsNullOrEmpty(saved.PdfPath));

        // The file store should contain the bytes, and they must start with %PDF.
        Assert.Single(files.Files);
        var bytes = files.Files.First().Value;
        Assert.True(bytes.Length > 200, $"PDF unexpectedly small: {bytes.Length} bytes");
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
