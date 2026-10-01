using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Documents;
using NgoFund.Domain.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class DocumentService(AppDbContext dbContext, IFileStorage fileStorage) : IDocumentService
{
    public async Task<DocumentDto> UploadAsync(
        Stream content, string fileName, string contentType, string documentType,
        Guid? applicantId, Guid? applicationId, Guid? donationId, Guid? paymentId, Guid? applicationGuarantorId,
        string? slotKey, string? description,
        CancellationToken cancellationToken, string? externalFileReference = null)
    {
        var ownerCount = new[] { applicantId, applicationId, donationId, paymentId, applicationGuarantorId }.Count(x => x is not null);
        if (ownerCount != 1)
        {
            throw new InvalidFileException("A document must be attached to exactly one owner.");
        }

        if (!Enum.TryParse<DocumentType>(documentType, out var parsedDocumentType))
        {
            throw new InvalidFileException($"'{documentType}' is not a recognized document type.");
        }

        if (slotKey is not null)
        {
            await EnsureSlotMatchesAsync(slotKey, parsedDocumentType, applicantId, applicationId, applicationGuarantorId, cancellationToken);
        }

        var stored = await DocumentFileValidator.ValidateAndStoreAsync(fileStorage, content, contentType, cancellationToken);

        var document = new Document
        {
            FileName = fileName,
            StorageKey = stored.StorageKey,
            ContentType = contentType,
            SizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            DocumentType = parsedDocumentType,
            ApplicantId = applicantId,
            ApplicationId = applicationId,
            DonationId = donationId,
            PaymentId = paymentId,
            ApplicationGuarantorId = applicationGuarantorId,
            SlotKey = slotKey,
            Description = description,
            ExternalFileReference = externalFileReference,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(document);
    }

    /// <summary>Validates a client-supplied <paramref name="slotKey"/> against the target
    /// application/guarantor's category manifest before the upload is accepted — the slot must
    /// exist for that category, the document type must be one it accepts, and the owner arm
    /// (application vs. guarantor) must match the slot's scope.
    ///
    /// v1.4 (B6): an applicant-owned upload can never carry a slotKey, and is rejected outright
    /// rather than validated. Unlike an application or a guarantor, an applicant has no single
    /// category to resolve a manifest against — an applicant can have applications across several
    /// categories (or none yet), so there is no category-specific slot list to check a client-
    /// supplied slotKey against. The alternative (deriving "a" category from, say, the applicant's
    /// most recent application) would be arbitrary and could silently validate against the wrong
    /// category's manifest. Applicant profile documents legitimately carry no slot key — see
    /// ApplicationCompletenessEvaluator.EvaluateSlots, which matches them by DocumentType alone —
    /// so rejecting a client-supplied one here is the coherent choice, not a gap.</summary>
    private async Task EnsureSlotMatchesAsync(
        string slotKey, DocumentType documentType, Guid? applicantId, Guid? applicationId, Guid? applicationGuarantorId, CancellationToken cancellationToken)
    {
        string categoryCode;
        DocumentSlotOwnerScope ownerScope;

        if (applicationId is not null)
        {
            categoryCode = await dbContext.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => a.ApplicationCategory.Code)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException("Application", applicationId.Value);
            ownerScope = DocumentSlotOwnerScope.Application;
        }
        else if (applicationGuarantorId is not null)
        {
            categoryCode = await dbContext.ApplicationGuarantors
                .Where(g => g.Id == applicationGuarantorId)
                .Select(g => g.Application.ApplicationCategory.Code)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EntityNotFoundException("ApplicationGuarantor", applicationGuarantorId.Value);
            ownerScope = DocumentSlotOwnerScope.Guarantor;
        }
        else if (applicantId is not null)
        {
            throw new DocumentSlotMismatchException($"slotKey '{slotKey}' was supplied on an applicant-owned upload; applicant profile documents cannot carry a slot key.");
        }
        else
        {
            throw new DocumentSlotMismatchException($"slotKey '{slotKey}' was supplied, but this upload isn't attached to an application or a guarantor.");
        }

        var slot = ApplicationRequirements.FindSlot(categoryCode, slotKey)
            ?? throw new DocumentSlotMismatchException($"'{slotKey}' is not a recognized document slot for category '{categoryCode}'.");

        if (!slot.AcceptedTypes.Contains(documentType))
        {
            throw new DocumentSlotMismatchException($"Document type '{documentType}' is not accepted for slot '{slotKey}'.");
        }

        if (slot.OwnerScope != ownerScope)
        {
            throw new DocumentSlotMismatchException($"Slot '{slotKey}' is {slot.OwnerScope}-scoped, not {ownerScope}-scoped.");
        }
    }

    public async Task<(Stream Content, DocumentDto Metadata)> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Document", id);

        var stream = await fileStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        return (stream, Map(document));
    }

    public async Task<IReadOnlyList<DocumentDto>> GetForApplicantAsync(Guid applicantId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicantId == applicantId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DocumentDto>> GetForApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicationId == applicationId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DocumentDto>> GetForGuarantorAsync(Guid guarantorId, CancellationToken cancellationToken)
    {
        var docs = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ApplicationGuarantorId == guarantorId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return docs.Select(Map).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Document", id);

        await fileStorage.DeleteAsync(document.StorageKey, cancellationToken);

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static DocumentDto Map(Document d) => new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType.ToString(), d.Description, d.UploadedAt, d.SlotKey);
}
