using Microsoft.EntityFrameworkCore;
using Npgsql;
using NgoFund.Application.Abstractions;
using NgoFund.Application.Intake;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Documents;
using NgoFund.Contracts.Intake;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// C6: the Infrastructure-side orchestrator behind <see cref="IGoogleFormIntakeService"/>. Every
/// write goes through the EXISTING applicant/application/details/document services — this class
/// only maps (via <see cref="GoogleFormSubmissionMapper"/>), looks up reference data, and sequences
/// the calls inside one transaction. <see cref="ICurrentUserService"/> already resolves to the
/// Google Form Intake system user for every request on this path (the "GoogleFormIntake" auth
/// scheme sets that principal), so created_by/changed_by/the activity feed need no special-casing
/// here.
/// </summary>
public class GoogleFormIntakeService(
    AppDbContext dbContext,
    IApplicantService applicantService,
    IFundApplicationService applicationService,
    IApplicationDetailsService detailsService,
    IDocumentService documentService) : IGoogleFormIntakeService
{
    // Fixed UTC+05:00, no DST observed in Pakistan since 2009 — DateTimeOffset with a literal
    // offset is simpler and just as correct as pulling in a TZ database lookup for one fixed zone.
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    public async Task<GoogleFormSubmissionResponse> SubmitAsync(GoogleFormSubmissionRequest request, CancellationToken cancellationToken)
    {
        var existing = await FindByFormResponseIdAsync(request.FormResponseId, cancellationToken);
        if (existing is not null)
        {
            return new GoogleFormSubmissionResponse(existing.Id, existing.ApplicationNumber, Created: false);
        }

        var mapped = GoogleFormSubmissionMapper.Map(request);

        await using var transaction = await dbContext.Database.BeginTransactionIfNoneAsync(cancellationToken);

        try
        {
            var (applicantId, notes) = await FindOrCreateApplicantAsync(mapped, cancellationToken);

            var category = await dbContext.ApplicationCategories.SingleOrDefaultAsync(c => c.Code == mapped.CategoryCode, cancellationToken)
                ?? throw new UnrecognizedGoogleFormDataException($"Application category '{mapped.CategoryCode}' is not configured.");
            var fund = await dbContext.FundCategories.SingleOrDefaultAsync(f => f.Code == mapped.FundCategoryCode, cancellationToken)
                ?? throw new UnrecognizedGoogleFormDataException($"Fund category '{mapped.FundCategoryCode}' is not configured.");

            var submittedAtPakistan = request.SubmittedAt.ToOffset(PakistanOffset);

            var application = await applicationService.CreateAsync(new CreateApplicationRequest(
                ApplicantId: applicantId,
                ApplicationCategoryId: category.Id,
                FundCategoryId: fund.Id,
                RequestedAmount: mapped.RequestedAmount,
                Priority: nameof(ApplicationPriority.Normal),
                ApplicationDate: DateOnly.FromDateTime(submittedAtPakistan.DateTime),
                Purpose: Truncate(mapped.Purpose, 1000),
                DeclaredMonthlyIncome: mapped.DeclaredMonthlyIncome,
                DeclaredHouseholdSize: mapped.DeclaredHouseholdSize,
                DeclaredEarningMembers: mapped.DeclaredEarningMembers,
                DeclaredResidentialAddress: mapped.DeclaredResidentialAddress,
                DeclaredBusinessAddress: mapped.DeclaredBusinessAddress,
                DeclaredHouseStatus: mapped.DeclaredHouseStatus,
                IntakeChannel: nameof(ApplicationIntakeChannel.GoogleForm),
                ExternalFormReference: request.FormResponseId,
                SubmittedAt: request.SubmittedAt,
                DeclarationAcceptedAt: mapped.DeclarationAcceptedAt,
                TermsAcceptedAt: null,
                TermsVersion: null),
                cancellationToken);

            await UpsertCategoryDetailsAsync(application.Id, mapped, cancellationToken);

            if (mapped.Guarantors.Count > 0)
            {
                await detailsService.ReplaceGuarantorsAsync(application.Id, new ReplaceApplicationGuarantorsRequest(
                    mapped.Guarantors.Select(g => new GuarantorEntry(
                        Id: null, g.SequenceNumber, g.MembershipNumber, g.FullName, g.FatherName, g.GrandfatherName,
                        g.Surname, g.Cnic, g.ResidentialAddress, g.BusinessAddress, g.BusinessNature,
                        PhoneHome: null, PhoneOffice: null, g.PhoneMobile, DeclarationAcceptedAt: null)).ToList()),
                    cancellationToken);
            }

            var allNotes = notes.Concat(mapped.Notes).ToList();
            if (allNotes.Count > 0)
            {
                await applicationService.AddRemarkAsync(application.Id, new AddRemarkRequest(
                    "Google Form intake notes:\n" + string.Join("\n", allNotes.Select(n => $"- {n}")), IsInternal: true),
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new GoogleFormSubmissionResponse(application.Id, application.ApplicationNumber, Created: true);
        }
        catch (DbUpdateException ex) when (IsExternalFormReferenceConflict(ex))
        {
            // Two concurrent posts for the same FormResponseId raced past the pre-check above —
            // the unique index on applications.external_form_reference caught it. The Postgres
            // transaction is now aborted (25P02) and every entity this request touched is still
            // tracked with its failed state — both must be cleared before any further query, or
            // that query fails too. Resolve to the row the other request just committed instead of
            // surfacing a 500.
            await transaction.DisposeAsync();
            dbContext.ChangeTracker.Clear();
            var winner = await FindByFormResponseIdAsync(request.FormResponseId, cancellationToken)
                ?? throw new InvalidOperationException("External form reference conflict, but no matching application was found.");
            return new GoogleFormSubmissionResponse(winner.Id, winner.ApplicationNumber, Created: false);
        }
    }

    public async Task<(DocumentDto Document, bool Created)> UploadDocumentAsync(
        string formResponseId, string questionTitle, string? questionHelpText, string driveFileId,
        string fileName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .Include(a => a.ApplicationCategory)
            .SingleOrDefaultAsync(a => a.ExternalFormReference == formResponseId, cancellationToken)
            ?? throw new EntityNotFoundException("Application", formResponseId);

        // IgnoreQueryFilters: ix_documents_external_file_reference is unique across ALL rows,
        // including soft-deleted ones. If staff soft-delete an intake document and Apps Script
        // re-sends it (its own retry, or a re-run of retryPending), the soft-delete filter would
        // hide the existing row from this lookup, UploadAsync would try to insert a duplicate, and
        // the unique index would reject it — a 500 Apps Script then retries forever. Treat a
        // soft-deleted match the same as a live one ("already received") rather than resurrecting it.
        var existingDocument = await dbContext.Documents.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(d => d.ExternalFileReference == driveFileId, cancellationToken);
        if (existingDocument is not null)
        {
            return (new DocumentDto(existingDocument.Id, existingDocument.FileName, existingDocument.ContentType,
                existingDocument.SizeBytes, existingDocument.DocumentType.ToString(), existingDocument.Description,
                existingDocument.UploadedAt, existingDocument.SlotKey), Created: false);
        }

        var manifestEntry = new GoogleFormFileManifestEntry(questionTitle, questionHelpText, driveFileId, fileName, contentType);
        var target = GoogleFormSubmissionMapper.ResolveFileTarget(application.ApplicationCategory.Code, manifestEntry);

        Guid? applicantId = null, applicationId = null, applicationGuarantorId = null;
        string documentType;
        string? slotKey;
        string? description;

        if (target is null)
        {
            // Unmapped — still stored (nothing is silently dropped), unslotted, for staff to re-slot.
            applicationId = application.Id;
            documentType = nameof(DocumentType.SupportingDocument);
            slotKey = null;
            description = $"Google Form intake: unmapped file for question '{questionTitle}'.";
        }
        else
        {
            documentType = target.DocumentType.ToString();
            slotKey = target.SlotKey;
            description = target.Description;

            switch (target.OwnerScope)
            {
                case DocumentSlotOwnerScope.Applicant:
                    applicantId = application.ApplicantId;
                    break;
                case DocumentSlotOwnerScope.Application:
                    applicationId = application.Id;
                    break;
                case DocumentSlotOwnerScope.Guarantor:
                    applicationGuarantorId = await dbContext.ApplicationGuarantors
                        .Where(g => g.ApplicationId == application.Id && g.SequenceNumber == target.GuarantorSequenceNumber)
                        .Select(g => (Guid?)g.Id)
                        .SingleOrDefaultAsync(cancellationToken);
                    if (applicationGuarantorId is null)
                    {
                        // The named guarantor row doesn't exist yet (e.g. the submission call is
                        // still in flight/retrying) — fall back to an unslotted application document
                        // rather than failing the upload outright.
                        applicationId = application.Id;
                        slotKey = null;
                        documentType = target.DocumentType.ToString();
                        description = $"Google Form intake: guarantor {target.GuarantorSequenceNumber} not found yet for slot '{target.SlotKey}'.";
                    }
                    break;
            }
        }

        var uploaded = await documentService.UploadAsync(
            content, fileName, contentType, documentType,
            applicantId, applicationId, donationId: null, paymentId: null, applicationGuarantorId,
            slotKey, description, cancellationToken, externalFileReference: driveFileId);
        return (uploaded, Created: true);
    }

    private async Task<FundApplication?> FindByFormResponseIdAsync(string formResponseId, CancellationToken cancellationToken) =>
        await dbContext.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.ExternalFormReference == formResponseId, cancellationToken);

    private static bool IsExternalFormReferenceConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName is not null && pg.ConstraintName.Contains("external_form_reference", StringComparison.OrdinalIgnoreCase);

    /// <summary>Applicant find-or-create by normalized CNIC (C6 step 2). A brand-new applicant is
    /// created via the existing <see cref="IApplicantService.CreateAsync"/> with no inline files and
    /// no gender. An existing applicant is never overwritten — only its currently-null fields are
    /// filled — and a name mismatch or a membership-number conflict with a DIFFERENT applicant is
    /// recorded as a note rather than silently applied.</summary>
    private async Task<(Guid ApplicantId, List<string> Notes)> FindOrCreateApplicantAsync(GoogleFormMappingResult mapped, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var existing = await dbContext.Applicants.SingleOrDefaultAsync(a => a.Cnic == mapped.Applicant.Cnic, cancellationToken);

        if (existing is null)
        {
            var created = await applicantService.CreateAsync(new CreateApplicantRequest(
                MembershipNumber: await ResolveMembershipNumberAsync(mapped.Applicant.MembershipNumber, existingApplicantId: null, notes, cancellationToken),
                FullName: mapped.Applicant.FullName,
                FatherOrHusbandName: mapped.Applicant.FatherOrHusbandName,
                Cnic: mapped.Applicant.Cnic,
                Gender: null,
                DateOfBirth: null,
                MaritalStatus: mapped.Applicant.MaritalStatus,
                Phone: mapped.Applicant.Phone,
                AlternatePhone: null,
                Email: mapped.Applicant.Email,
                Address: mapped.Applicant.Address,
                City: null,
                District: null,
                Province: null,
                Occupation: mapped.Applicant.Occupation,
                MonthlyIncome: mapped.Applicant.MonthlyIncome,
                DependentsCount: null,
                HouseholdSize: mapped.Applicant.HouseholdSize,
                Notes: null,
                GrandfatherName: mapped.Applicant.GrandfatherName,
                Surname: mapped.Applicant.Surname,
                AncestralVillage: mapped.Applicant.AncestralVillage,
                FatherMembershipNumber: mapped.Applicant.FatherMembershipNumber,
                WhatsappNumber: mapped.Applicant.WhatsappNumber),
                cancellationToken);

            return (created.Id, notes);
        }

        if (!string.Equals(existing.FullName.Trim(), mapped.Applicant.FullName.Trim(), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(mapped.Applicant.FullName))
        {
            notes.Add($"Form name '{mapped.Applicant.FullName}' differs from applicant profile name '{existing.FullName}' on file (CNIC {existing.Cnic}); profile name kept.");
        }

        var membershipNumber = existing.MembershipNumber
            ?? await ResolveMembershipNumberAsync(mapped.Applicant.MembershipNumber, existing.Id, notes, cancellationToken);

        await applicantService.UpdateAsync(existing.Id, new UpdateApplicantRequest(
            MembershipNumber: membershipNumber,
            FullName: existing.FullName,
            FatherOrHusbandName: existing.FatherOrHusbandName ?? mapped.Applicant.FatherOrHusbandName,
            Cnic: existing.Cnic,
            Gender: existing.Gender?.ToString(),
            DateOfBirth: existing.DateOfBirth,
            MaritalStatus: existing.MaritalStatus?.ToString() ?? mapped.Applicant.MaritalStatus,
            Phone: existing.Phone ?? mapped.Applicant.Phone,
            AlternatePhone: existing.AlternatePhone,
            Email: existing.Email ?? mapped.Applicant.Email,
            Address: existing.Address ?? mapped.Applicant.Address,
            City: existing.City,
            District: existing.District,
            Province: existing.Province,
            Occupation: existing.Occupation ?? mapped.Applicant.Occupation,
            MonthlyIncome: existing.MonthlyIncome ?? mapped.Applicant.MonthlyIncome,
            DependentsCount: existing.DependentsCount,
            HouseholdSize: existing.HouseholdSize ?? mapped.Applicant.HouseholdSize,
            IsBlacklisted: existing.IsBlacklisted,
            BlacklistReason: existing.BlacklistReason,
            Notes: existing.Notes,
            GrandfatherName: existing.GrandfatherName ?? mapped.Applicant.GrandfatherName,
            Surname: existing.Surname ?? mapped.Applicant.Surname,
            AncestralVillage: existing.AncestralVillage ?? mapped.Applicant.AncestralVillage,
            FatherMembershipNumber: existing.FatherMembershipNumber ?? mapped.Applicant.FatherMembershipNumber,
            WhatsappNumber: existing.WhatsappNumber ?? mapped.Applicant.WhatsappNumber),
            cancellationToken);

        return (existing.Id, notes);
    }

    /// <summary>A membership number already used by a DIFFERENT applicant is left null (with a
    /// note naming the conflict) rather than violating the partial-unique-index or silently
    /// stealing the number from its rightful owner.</summary>
    private async Task<string?> ResolveMembershipNumberAsync(string? membershipNumber, Guid? existingApplicantId, List<string> notes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(membershipNumber))
        {
            return null;
        }

        // IgnoreQueryFilters: a membership number held by a soft-deleted applicant still trips
        // the partial unique index, so it must degrade to null + a note the same as a live conflict.
        var conflict = await dbContext.Applicants.IgnoreQueryFilters()
            .Where(a => a.MembershipNumber == membershipNumber && a.Id != existingApplicantId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (conflict is not null)
        {
            notes.Add($"Membership number '{membershipNumber}' is already used by a different applicant; left blank for staff review.");
            return null;
        }

        return membershipNumber;
    }

    private async Task UpsertCategoryDetailsAsync(Guid applicationId, GoogleFormMappingResult mapped, CancellationToken cancellationToken)
    {
        switch (mapped.CategoryCode)
        {
            case "HOUSE_RENT":
                await detailsService.UpsertHousingDetailsAsync(applicationId, mapped.Housing!, cancellationToken, enforceRequiredFields: false);
                break;
            case "SHAADI":
                await detailsService.UpsertMarriageDetailsAsync(applicationId, mapped.Marriage!, cancellationToken, enforceRequiredFields: false);
                break;
            case "ROZGAR":
                await detailsService.UpsertBusinessLoanDetailsAsync(applicationId, mapped.BusinessLoan!, cancellationToken, enforceRequiredFields: false);
                break;
            case "EDUCATION":
                await detailsService.UpsertEducationDetailsAsync(applicationId, mapped.Education!, cancellationToken, enforceRequiredFields: false);
                break;
            case "HEALTH":
                await detailsService.UpsertHealthDetailsAsync(applicationId, mapped.Health!, cancellationToken, enforceRequiredFields: false);
                break;
            // OTHER (A3) has no details table — every field maps onto applicants/applications.
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
