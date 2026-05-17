using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.Documents;
using Workshop.Application.Features.Photos;
using Workshop.Application.Features.Repairs;
using Workshop.Domain.Enums;

namespace Workshop.Application.Tests;

public class PhotoAndDocumentHandlersTests
{
    private static async Task<(Guid caseId, Guid assessmentId)> SeedCaseWithAssessmentAsync(
        Workshop.Infrastructure.Persistence.WorkshopDbContext db)
    {
        var (caseId, panelAllowed, _) = await AssessmentHandlersTests.SeedCaseWithPanelsAsync(db);
        var assessmentId = await new UpsertAssessmentHandler(db, new AllowedOpsValidator(db)).Handle(
            new UpsertAssessmentCommand(caseId, new AssessmentUpsertDto(
                DateOnly.FromDateTime(DateTime.Today), false, null, null, 100m,
                DateOnly.FromDateTime(DateTime.Today), false, null,
                new[] { new WorkItemUpsertDto(null, panelAllowed, "x",
                    50m, null, null, null, null, null, null, null, null, null, null) })),
            default);
        return (caseId, assessmentId);
    }

    private static MemoryStream FakeFile(string contents = "hello") =>
        new MemoryStream(Encoding.UTF8.GetBytes(contents));

    [Fact]
    public async Task UploadPhoto_ToAssessment_StoresAndPersistsRow()
    {
        await using var db = TestDb.NewContext();
        var (_, assessmentId) = await SeedCaseWithAssessmentAsync(db);
        var files = new FakeFileStore();
        var user = new TestCurrentUser();
        var handler = new UploadPhotoHandler(db, files, user);

        var photoId = await handler.Handle(new UploadPhotoCommand(
            new UploadPhotoInput(PhotoOwnerKind.Assessment, assessmentId,
                PhotoPhase.Damage, "front.jpg", "image/jpeg", 5, null),
            FakeFile("front")), default);

        var saved = await db.Photos.AsNoTracking().FirstAsync(p => p.Id == photoId);
        Assert.Equal(PhotoPhase.Damage, saved.Phase);
        Assert.Equal(assessmentId, saved.AssessmentId);
        Assert.Null(saved.RepairId);
        Assert.Equal(5, saved.SizeBytes);
        Assert.Single(files.Files);
    }

    [Fact]
    public async Task UploadPhoto_FailsWhenOwnerMissing()
    {
        await using var db = TestDb.NewContext();
        await SeedCaseWithAssessmentAsync(db);
        var handler = new UploadPhotoHandler(db, new FakeFileStore(), new TestCurrentUser());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new UploadPhotoCommand(
                new UploadPhotoInput(PhotoOwnerKind.Assessment, Guid.NewGuid(),
                    PhotoPhase.Damage, "x.jpg", "image/jpeg", 1, null), FakeFile()), default));
    }

    [Fact]
    public async Task UploadPhoto_FailsWithoutAuth()
    {
        await using var db = TestDb.NewContext();
        var (_, assessmentId) = await SeedCaseWithAssessmentAsync(db);
        var handler = new UploadPhotoHandler(db, new FakeFileStore(),
            TestCurrentUser.Anonymous());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UploadPhotoCommand(
                new UploadPhotoInput(PhotoOwnerKind.Assessment, assessmentId,
                    PhotoPhase.Damage, "x.jpg", "image/jpeg", 1, null), FakeFile()), default));
    }

    [Fact]
    public async Task DeletePhoto_RemovesFileAndRow()
    {
        await using var db = TestDb.NewContext();
        var (_, assessmentId) = await SeedCaseWithAssessmentAsync(db);
        var files = new FakeFileStore();
        var user = new TestCurrentUser();
        var upload = new UploadPhotoHandler(db, files, user);
        var photoId = await upload.Handle(new UploadPhotoCommand(
            new UploadPhotoInput(PhotoOwnerKind.Assessment, assessmentId,
                PhotoPhase.Intake, "x.jpg", "image/jpeg", 5, null), FakeFile()), default);

        await new DeletePhotoHandler(db, files).Handle(new DeletePhotoCommand(photoId), default);

        Assert.Empty(files.Files);
        Assert.False(await db.Photos.AnyAsync(p => p.Id == photoId));
    }

    [Fact]
    public async Task GetPhotosForOwner_FiltersByPhase()
    {
        await using var db = TestDb.NewContext();
        var (_, assessmentId) = await SeedCaseWithAssessmentAsync(db);
        var files = new FakeFileStore();
        var user = new TestCurrentUser();
        var upload = new UploadPhotoHandler(db, files, user);
        await upload.Handle(new UploadPhotoCommand(
            new UploadPhotoInput(PhotoOwnerKind.Assessment, assessmentId,
                PhotoPhase.Intake, "a.jpg", "image/jpeg", 1, null), FakeFile("a")), default);
        await upload.Handle(new UploadPhotoCommand(
            new UploadPhotoInput(PhotoOwnerKind.Assessment, assessmentId,
                PhotoPhase.Damage, "b.jpg", "image/jpeg", 1, null), FakeFile("b")), default);

        var intake = await new GetPhotosForOwnerHandler(db).Handle(
            new GetPhotosForOwnerQuery(PhotoOwnerKind.Assessment, assessmentId, PhotoPhase.Intake), default);
        var all = await new GetPhotosForOwnerHandler(db).Handle(
            new GetPhotosForOwnerQuery(PhotoOwnerKind.Assessment, assessmentId), default);

        Assert.Single(intake);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task UploadDocument_StoresAndPersistsRow()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _) = await SeedCaseWithAssessmentAsync(db);
        var files = new FakeFileStore();
        var user = new TestCurrentUser();
        var handler = new UploadDocumentHandler(db, files, user);

        var docId = await handler.Handle(new UploadDocumentCommand(
            new UploadDocumentInput(DocumentOwnerKind.InsuranceCase, caseId,
                DocumentType.CaseForm, "form.pdf", "application/pdf", 4),
            FakeFile("form")), default);

        var saved = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.Equal(DocumentType.CaseForm, saved.DocumentType);
        Assert.Equal(caseId, saved.InsuranceCaseId);
        Assert.False(saved.SentToInsurance);
        Assert.Single(files.Files);
    }

    [Fact]
    public async Task DeleteDocument_RefusesWhenAlreadySent()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _) = await SeedCaseWithAssessmentAsync(db);
        var files = new FakeFileStore();
        var user = new TestCurrentUser();
        var docId = await new UploadDocumentHandler(db, files, user).Handle(
            new UploadDocumentCommand(new UploadDocumentInput(
                DocumentOwnerKind.InsuranceCase, caseId, DocumentType.CaseForm,
                "x.pdf", "application/pdf", 4), FakeFile()), default);

        await new MarkDocumentSentToInsuranceHandler(db, TimeProvider.System).Handle(
            new MarkDocumentSentToInsuranceCommand(docId, true), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeleteDocumentHandler(db, files).Handle(new DeleteDocumentCommand(docId), default));
    }

    [Fact]
    public async Task MarkDocumentSentToInsurance_TogglesFlagAndTimestamp()
    {
        await using var db = TestDb.NewContext();
        var (caseId, _) = await SeedCaseWithAssessmentAsync(db);
        var docId = await new UploadDocumentHandler(db, new FakeFileStore(), new TestCurrentUser()).Handle(
            new UploadDocumentCommand(new UploadDocumentInput(
                DocumentOwnerKind.InsuranceCase, caseId, DocumentType.Invoice,
                "x.pdf", "application/pdf", 4), FakeFile()), default);

        var handler = new MarkDocumentSentToInsuranceHandler(db, TimeProvider.System);
        await handler.Handle(new MarkDocumentSentToInsuranceCommand(docId, true), default);

        var saved = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.True(saved.SentToInsurance);
        Assert.NotNull(saved.SentToInsuranceAt);

        await handler.Handle(new MarkDocumentSentToInsuranceCommand(docId, false), default);
        saved = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.False(saved.SentToInsurance);
        Assert.Null(saved.SentToInsuranceAt);
    }

    [Fact]
    public async Task UploadValidator_RejectsZeroAndOversized()
    {
        var validator = new UploadDocumentValidator();
        var zero = new UploadDocumentCommand(
            new UploadDocumentInput(DocumentOwnerKind.InsuranceCase, Guid.NewGuid(),
                DocumentType.CaseForm, "x.pdf", "application/pdf", 0), new MemoryStream());
        var big = zero with { Input = zero.Input with { SizeBytes = 30L * 1024 * 1024 } };

        Assert.False((await validator.ValidateAsync(zero)).IsValid);
        Assert.False((await validator.ValidateAsync(big)).IsValid);
    }
}
