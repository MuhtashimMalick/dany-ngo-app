using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Identity;
using NgoFund.Infrastructure.Persistence;
using NgoFund.Infrastructure.Persistence.Auditing;

namespace NgoFund.UnitTests.Services;

/// <summary>
/// One test per row of the activity-narration table (see the M-something plan doc for the exact
/// sentences), plus null-return coverage for entities that must never surface in the client-facing
/// feed. No database is ever opened — <see cref="AppDbContext"/>'s change tracker works against
/// Attach/Add'd entities without a connection, and <see cref="ActivityNarrator.Narrate"/> only ever
/// reads already-tracked state, per its own hard constraint.
/// </summary>
public class ActivityNarratorTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

    private static EntityEntry Modify<TEntity>(AppDbContext db, TEntity entity, params (string Property, object? Value)[] changes)
        where TEntity : class
    {
        var entry = db.Attach(entity);
        foreach (var (property, value) in changes)
        {
            entry.Property(property).CurrentValue = value;
        }

        return entry;
    }

    // --- Donor ---

    [Fact]
    public void Donor_Create()
    {
        var db = CreateDb();
        var donor = new Donor { DonorCode = "DNR-2026-00001", FullName = "Jane Doe" };

        var narration = ActivityNarrator.Narrate(db.Add(donor), "Create");

        Assert.Equal("Donor \"DNR-2026-00001\" (Jane Doe) was added", narration!.Summary);
        Assert.Equal(ActivityVerb.Created, narration.Verb);
        Assert.Equal("DNR-2026-00001", narration.EntityNumber);
    }

    [Fact]
    public void Donor_Update()
    {
        var db = CreateDb();
        var donor = new Donor { DonorCode = "DNR-2026-00001", FullName = "Jane Doe" };
        var entry = Modify(db, donor, (nameof(Donor.FullName), "Jane Smith"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Donor \"DNR-2026-00001\" was updated", narration!.Summary);
        Assert.Equal(ActivityVerb.Updated, narration.Verb);
    }

    [Fact]
    public void Donor_SoftDelete()
    {
        var db = CreateDb();
        var donor = new Donor { DonorCode = "DNR-2026-00001", FullName = "Jane Doe" };
        var entry = db.Attach(donor);

        var narration = ActivityNarrator.Narrate(entry, "SoftDelete");

        Assert.Equal("Donor \"DNR-2026-00001\" was deleted", narration!.Summary);
        Assert.Equal(ActivityVerb.Deleted, narration.Verb);
    }

    // --- Donation ---

    [Fact]
    public void Donation_Create()
    {
        var db = CreateDb();
        var donation = new Donation { DonationNumber = "DON-2026-00001", Amount = 5000m };

        var narration = ActivityNarrator.Narrate(db.Add(donation), "Create");

        Assert.Equal("Donation \"DON-2026-00001\" was recorded", narration!.Summary);
        Assert.Equal(ActivityVerb.Recorded, narration.Verb);
    }

    [Fact]
    public void Donation_StatusVoided_Update()
    {
        var db = CreateDb();
        var donation = new Donation { DonationNumber = "DON-2026-00001", Amount = 5000m, Status = DonationStatus.Confirmed };
        var entry = Modify(db, donation, (nameof(Donation.Status), DonationStatus.Voided));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Donation \"DON-2026-00001\" was voided", narration!.Summary);
        Assert.Equal(ActivityVerb.Voided, narration.Verb);
    }

    [Fact]
    public void Donation_OtherUpdate()
    {
        var db = CreateDb();
        var donation = new Donation { DonationNumber = "DON-2026-00001", Amount = 5000m };
        var entry = Modify(db, donation, (nameof(Donation.Notes), "corrected bank name"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Donation \"DON-2026-00001\" was updated", narration!.Summary);
        Assert.Equal(ActivityVerb.Updated, narration.Verb);
    }

    // --- Applicant ---

    [Fact]
    public void Applicant_Create()
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1", MembershipNumber = "MEM-001" };

        var narration = ActivityNarrator.Narrate(db.Add(applicant), "Create");

        Assert.Equal("Applicant \"Ali Khan\" was added", narration!.Summary);
        Assert.Equal(ActivityVerb.Created, narration.Verb);
        Assert.Equal("MEM-001", narration.EntityNumber);
    }

    [Fact]
    public void Applicant_Blacklisted()
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1", IsBlacklisted = false };
        var entry = Modify(db, applicant, (nameof(Applicant.IsBlacklisted), true));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Applicant \"Ali Khan\" was blacklisted", narration!.Summary);
        Assert.Equal(ActivityVerb.Blacklisted, narration.Verb);
    }

    [Fact]
    public void Applicant_RemovedFromBlacklist()
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1", IsBlacklisted = true };
        var entry = Modify(db, applicant, (nameof(Applicant.IsBlacklisted), false));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Applicant \"Ali Khan\" was removed from the blacklist", narration!.Summary);
    }

    [Fact]
    public void Applicant_OtherUpdate()
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1" };
        var entry = Modify(db, applicant, (nameof(Applicant.Phone), "03001234567"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Applicant \"Ali Khan\" was updated", narration!.Summary);
    }

    [Fact]
    public void Applicant_SoftDelete()
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1" };
        var entry = db.Attach(applicant);

        var narration = ActivityNarrator.Narrate(entry, "SoftDelete");

        Assert.Equal("Applicant \"Ali Khan\" was deleted", narration!.Summary);
    }

    // --- FundApplication ---

    [Fact]
    public void FundApplication_Create()
    {
        var db = CreateDb();
        var application = new FundApplication { ApplicationNumber = "APP-2026-00001", RequestedAmount = 1000m };

        var narration = ActivityNarrator.Narrate(db.Add(application), "Create");

        Assert.Equal("Application \"APP-2026-00001\" was created", narration!.Summary);
        Assert.Equal(ActivityVerb.Created, narration.Verb);
    }

    [Fact]
    public void FundApplication_StatusChanged()
    {
        var db = CreateDb();
        var application = new FundApplication { ApplicationNumber = "APP-2026-00001", RequestedAmount = 1000m, Status = ApplicationStatus.Pending };
        var entry = Modify(db, application, (nameof(FundApplication.Status), ApplicationStatus.UnderReview));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Application \"APP-2026-00001\" status changed from Pending to UnderReview", narration!.Summary);
        Assert.Equal(ActivityVerb.StatusChanged, narration.Verb);
    }

    [Fact]
    public void FundApplication_OtherUpdate()
    {
        var db = CreateDb();
        var application = new FundApplication { ApplicationNumber = "APP-2026-00001", RequestedAmount = 1000m };
        var entry = Modify(db, application, (nameof(FundApplication.Purpose), "Updated purpose text"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Application \"APP-2026-00001\" was updated", narration!.Summary);
    }

    // --- Payment ---

    [Fact]
    public void Payment_Create()
    {
        var db = CreateDb();
        var payment = new Payment { PaymentNumber = "PAY-2026-00001", Amount = 500m };

        var narration = ActivityNarrator.Narrate(db.Add(payment), "Create");

        Assert.Equal("Payment \"PAY-2026-00001\" was recorded", narration!.Summary);
        Assert.Equal(ActivityVerb.Recorded, narration.Verb);
    }

    [Fact]
    public void Payment_StatusVoided_Update()
    {
        var db = CreateDb();
        var payment = new Payment { PaymentNumber = "PAY-2026-00001", Amount = 500m, Status = PaymentStatus.Completed };
        var entry = Modify(db, payment, (nameof(Payment.Status), PaymentStatus.Voided));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Payment \"PAY-2026-00001\" was voided", narration!.Summary);
        Assert.Equal(ActivityVerb.Voided, narration.Verb);
    }

    // --- LoanAgreement ---

    [Fact]
    public void LoanAgreement_Create_WithoutTrackedApplication_NoSuffix()
    {
        var db = CreateDb();
        var loan = new LoanAgreement { LoanNumber = "LOAN-2026-00001", PrincipalAmount = 1000m };

        var narration = ActivityNarrator.Narrate(db.Add(loan), "Create");

        Assert.Equal("Loan agreement \"LOAN-2026-00001\" was created", narration!.Summary);
        Assert.Equal(ActivityVerb.Created, narration.Verb);
    }

    [Fact]
    public void LoanAgreement_Create_WithTrackedApplication_AppendsApplicationNumber()
    {
        var db = CreateDb();
        var application = new FundApplication { ApplicationNumber = "APP-2026-00001", RequestedAmount = 1000m };
        db.Attach(application);
        var loan = new LoanAgreement { LoanNumber = "LOAN-2026-00001", PrincipalAmount = 1000m, ApplicationId = application.Id, Application = application };

        var narration = ActivityNarrator.Narrate(db.Add(loan), "Create");

        Assert.Equal("Loan agreement \"LOAN-2026-00001\" was created for application \"APP-2026-00001\"", narration!.Summary);
    }

    [Fact]
    public void LoanAgreement_Cancelled()
    {
        var db = CreateDb();
        var loan = new LoanAgreement { LoanNumber = "LOAN-2026-00001", PrincipalAmount = 1000m, Status = LoanAgreementStatus.Active };
        var entry = Modify(db, loan, (nameof(LoanAgreement.Status), LoanAgreementStatus.Cancelled));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Loan agreement \"LOAN-2026-00001\" was cancelled", narration!.Summary);
        Assert.Equal(ActivityVerb.Cancelled, narration.Verb);
    }

    [Fact]
    public void LoanAgreement_WrittenOff()
    {
        var db = CreateDb();
        var loan = new LoanAgreement { LoanNumber = "LOAN-2026-00001", PrincipalAmount = 1000m, Status = LoanAgreementStatus.Active };
        var entry = Modify(db, loan, (nameof(LoanAgreement.Status), LoanAgreementStatus.WrittenOff));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Loan agreement \"LOAN-2026-00001\" was written off", narration!.Summary);
        Assert.Equal(ActivityVerb.WrittenOff, narration.Verb);
    }

    // --- LoanRepayment ---

    [Fact]
    public void LoanRepayment_Create()
    {
        var db = CreateDb();
        var repayment = new LoanRepayment { RepaymentNumber = "LRP-2026-00001", Amount = 200m, ReceivedFromName = "Ali Khan" };

        var narration = ActivityNarrator.Narrate(db.Add(repayment), "Create");

        Assert.Equal("Loan repayment \"LRP-2026-00001\" was recorded", narration!.Summary);
        Assert.Equal(ActivityVerb.Recorded, narration.Verb);
    }

    [Fact]
    public void LoanRepayment_StatusVoided_Update()
    {
        var db = CreateDb();
        var repayment = new LoanRepayment { RepaymentNumber = "LRP-2026-00001", Amount = 200m, ReceivedFromName = "Ali Khan", Status = LoanRepaymentStatus.Completed };
        var entry = Modify(db, repayment, (nameof(LoanRepayment.Status), LoanRepaymentStatus.Voided));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Loan repayment \"LRP-2026-00001\" was voided", narration!.Summary);
        Assert.Equal(ActivityVerb.Voided, narration.Verb);
    }

    // --- ApplicationUser ---

    [Fact]
    public void ApplicationUser_Create()
    {
        var db = CreateDb();
        var user = new ApplicationUser { FullName = "Ayesha Malik" };

        var narration = ActivityNarrator.Narrate(db.Add(user), "Create");

        Assert.Equal("User \"Ayesha Malik\" was created", narration!.Summary);
        Assert.Equal(ActivityVerb.Created, narration.Verb);
    }

    [Fact]
    public void ApplicationUser_Deactivated()
    {
        var db = CreateDb();
        // ApplicationUser.Id has no default-Guid initializer (unlike BaseEntity.Id) — Attach on a
        // still-Guid.Empty key tracks the entry as Added, not Unchanged, so IsModified never fires.
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik", IsActive = true };
        var entry = Modify(db, user, (nameof(ApplicationUser.IsActive), false));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("User \"Ayesha Malik\" was deactivated", narration!.Summary);
        Assert.Equal(ActivityVerb.Deactivated, narration.Verb);
    }

    [Fact]
    public void ApplicationUser_Reactivated()
    {
        var db = CreateDb();
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik", IsActive = false };
        var entry = Modify(db, user, (nameof(ApplicationUser.IsActive), true));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("User \"Ayesha Malik\" was reactivated", narration!.Summary);
        Assert.Equal(ActivityVerb.Reactivated, narration.Verb);
    }

    [Fact]
    public void ApplicationUser_PasswordChanged()
    {
        var db = CreateDb();
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik", PasswordHash = "old-hash" };
        var entry = Modify(db, user, (nameof(ApplicationUser.PasswordHash), "new-hash"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Password for user \"Ayesha Malik\" was changed", narration!.Summary);
    }

    [Fact]
    public void ApplicationUser_OtherUpdate()
    {
        var db = CreateDb();
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik", Designation = "Officer" };
        var entry = Modify(db, user, (nameof(ApplicationUser.Designation), "Senior Officer"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("User \"Ayesha Malik\" was updated", narration!.Summary);
    }

    // --- FundCategory ---

    [Fact]
    public void FundCategory_Create()
    {
        var db = CreateDb();
        var fund = new FundCategory { Code = "ZAKAT", Name = "Zakat Fund" };

        var narration = ActivityNarrator.Narrate(db.Add(fund), "Create");

        Assert.Equal("Fund \"Zakat Fund\" was added", narration!.Summary);
    }

    [Fact]
    public void FundCategory_Update()
    {
        var db = CreateDb();
        var fund = new FundCategory { Code = "ZAKAT", Name = "Zakat Fund" };
        var entry = Modify(db, fund, (nameof(FundCategory.Description), "Updated description"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Fund \"Zakat Fund\" was updated", narration!.Summary);
    }

    [Fact]
    public void FundCategory_SoftDelete()
    {
        var db = CreateDb();
        var fund = new FundCategory { Code = "ZAKAT", Name = "Zakat Fund" };
        var entry = db.Attach(fund);

        var narration = ActivityNarrator.Narrate(entry, "SoftDelete");

        Assert.Equal("Fund \"Zakat Fund\" was deleted", narration!.Summary);
    }

    // --- ApplicationCategory ---

    [Fact]
    public void ApplicationCategory_Create()
    {
        var db = CreateDb();
        var category = new ApplicationCategory { Code = "HEALTH", Name = "Health" };

        var narration = ActivityNarrator.Narrate(db.Add(category), "Create");

        Assert.Equal("Application category \"Health\" was added", narration!.Summary);
    }

    [Fact]
    public void ApplicationCategory_Update()
    {
        var db = CreateDb();
        var category = new ApplicationCategory { Code = "HEALTH", Name = "Health" };
        var entry = Modify(db, category, (nameof(ApplicationCategory.DefaultMaxAmount), 50000m));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Application category \"Health\" was updated", narration!.Summary);
    }

    [Fact]
    public void ApplicationCategory_SoftDelete()
    {
        var db = CreateDb();
        var category = new ApplicationCategory { Code = "HEALTH", Name = "Health" };
        var entry = db.Attach(category);

        var narration = ActivityNarrator.Narrate(entry, "SoftDelete");

        Assert.Equal("Application category \"Health\" was deleted", narration!.Summary);
    }

    // --- AppSetting ---

    [Fact]
    public void AppSetting_Update()
    {
        var db = CreateDb();
        var setting = new AppSetting { Key = "OrgName", Value = "Old Name" };
        var entry = Modify(db, setting, (nameof(AppSetting.Value), "New Name"));

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("Setting \"OrgName\" was updated", narration!.Summary);
    }

    // --- RolePermission ---

    [Fact]
    public void RolePermission_Create_Granted()
    {
        var db = CreateDb();
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "Accounts Manager" };
        var permission = new Permission { Id = Guid.NewGuid(), Code = "donations.create", Module = "donations", DisplayName = "Record Donations" };
        db.Attach(role);
        db.Attach(permission);
        var rolePermission = new RolePermission { RoleId = role.Id, Role = role, PermissionId = permission.Id, Permission = permission };

        var narration = ActivityNarrator.Narrate(db.Add(rolePermission), "Create");

        Assert.Equal("Role \"Accounts Manager\" was granted permission \"Record Donations\"", narration!.Summary);
        Assert.Equal(ActivityVerb.PermissionGranted, narration.Verb);
    }

    [Fact]
    public void RolePermission_Delete_Revoked()
    {
        var db = CreateDb();
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "Accounts Manager" };
        var permission = new Permission { Id = Guid.NewGuid(), Code = "donations.create", Module = "donations", DisplayName = "Record Donations" };
        db.Attach(role);
        db.Attach(permission);
        var rolePermission = new RolePermission { RoleId = role.Id, Role = role, PermissionId = permission.Id, Permission = permission };
        var entry = db.Attach(rolePermission);

        var narration = ActivityNarrator.Narrate(entry, "Delete");

        Assert.Equal("Role \"Accounts Manager\" had permission \"Record Donations\" revoked", narration!.Summary);
        Assert.Equal(ActivityVerb.PermissionRevoked, narration.Verb);
    }

    [Fact]
    public void RolePermission_Create_WithoutTrackedNavigations_ReturnsNull()
    {
        var db = CreateDb();
        var rolePermission = new RolePermission { RoleId = Guid.NewGuid(), PermissionId = Guid.NewGuid() };

        // Never queries the DB to resolve names — an untracked Role/Permission means no narration.
        Assert.Null(ActivityNarrator.Narrate(db.Add(rolePermission), "Create"));
    }

    // --- Entities that must never narrate ---

    [Fact]
    public void FundTransaction_NeverNarrated()
    {
        var db = CreateDb();
        var transaction = new FundTransaction
        {
            FundCategoryId = Guid.NewGuid(),
            Direction = TransactionDirection.Credit,
            Amount = 100m,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceType = TransactionReferenceType.Donation,
            ReferenceId = Guid.NewGuid(),
        };

        Assert.Null(ActivityNarrator.Narrate(db.Add(transaction), "Create"));
    }

    [Fact]
    public void ApplicationStatusHistory_NeverNarrated()
    {
        var db = CreateDb();
        var history = new ApplicationStatusHistory { ApplicationId = Guid.NewGuid(), ToStatus = ApplicationStatus.Approved, ChangedAt = DateTimeOffset.UtcNow };

        Assert.Null(ActivityNarrator.Narrate(db.Add(history), "Create"));
    }

    [Fact]
    public void Document_NeverNarrated()
    {
        var db = CreateDb();
        var document = new Document
        {
            FileName = "cnic.png",
            StorageKey = Guid.NewGuid().ToString(),
            ContentType = "image/png",
            SizeBytes = 100,
            Sha256 = "abc",
            DocumentType = DocumentType.CnicFront,
            ApplicantId = Guid.NewGuid(),
            UploadedAt = DateTimeOffset.UtcNow,
        };

        Assert.Null(ActivityNarrator.Narrate(db.Add(document), "Create"));
    }

    // --- Document (applicant profile document replace, v1.6) ---

    private static (AppDbContext Db, Applicant Applicant, Document Document) CreateTrackedProfileDocument(DocumentType documentType)
    {
        var db = CreateDb();
        var applicant = new Applicant { FullName = "Ali Khan", Cnic = "12345-1234567-1", MembershipNumber = "MEM-001" };
        db.Attach(applicant);
        var document = new Document
        {
            FileName = "old.png",
            StorageKey = "old-key",
            ContentType = "image/png",
            SizeBytes = 100,
            Sha256 = new string('a', 64),
            DocumentType = documentType,
            ApplicantId = applicant.Id,
            Applicant = applicant,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        db.Attach(document);
        return (db, applicant, document);
    }

    [Theory]
    [InlineData(DocumentType.ApplicantPhoto, "Profile photo")]
    [InlineData(DocumentType.CnicFront, "CNIC (Front)")]
    [InlineData(DocumentType.CnicBack, "CNIC (Back)")]
    [InlineData(DocumentType.MembershipCard, "Jamaat Membership Card")]
    public void Document_ProfileDocumentReplaced_NarratesExpectedSentence(DocumentType documentType, string expectedDisplayName)
    {
        var (db, applicant, document) = CreateTrackedProfileDocument(documentType);
        var entry = db.Entry(document);
        entry.Property(nameof(Document.StorageKey)).CurrentValue = "new-key";

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal($"{expectedDisplayName} for applicant \"{applicant.FullName}\" was replaced", narration!.Summary);
        Assert.Equal("Applicant", narration.EntityLabel);
        Assert.Equal(applicant.MembershipNumber, narration.EntityNumber);
        Assert.Equal(ActivityVerb.Updated, narration.Verb);
    }

    [Fact]
    public void Document_ProfileDocumentReplaced_ApplicantNotTracked_UsesFallbackSentence()
    {
        var db = CreateDb();
        var document = new Document
        {
            FileName = "old.png",
            StorageKey = "old-key",
            ContentType = "image/png",
            SizeBytes = 100,
            Sha256 = new string('a', 64),
            DocumentType = DocumentType.CnicFront,
            ApplicantId = Guid.NewGuid(), // not tracked
            UploadedAt = DateTimeOffset.UtcNow,
        };
        var entry = db.Attach(document);
        entry.Property(nameof(Document.StorageKey)).CurrentValue = "new-key";

        var narration = ActivityNarrator.Narrate(entry, "Update");

        Assert.Equal("An applicant's CNIC (Front) was replaced", narration!.Summary);
        Assert.Null(narration.EntityNumber);
    }

    [Fact]
    public void Document_Create_NotNarrated()
    {
        var (db, _, document) = CreateTrackedProfileDocument(DocumentType.CnicFront);

        Assert.Null(ActivityNarrator.Narrate(db.Entry(document), "Create"));
    }

    [Fact]
    public void Document_Delete_NotNarrated()
    {
        var (db, _, document) = CreateTrackedProfileDocument(DocumentType.CnicFront);

        Assert.Null(ActivityNarrator.Narrate(db.Entry(document), "Delete"));
    }

    [Fact]
    public void Document_DescriptionOnlyUpdate_NotNarrated()
    {
        var (db, _, document) = CreateTrackedProfileDocument(DocumentType.CnicFront);
        var entry = db.Entry(document);
        entry.Property(nameof(Document.Description)).CurrentValue = "corrected note";

        Assert.Null(ActivityNarrator.Narrate(entry, "Update"));
    }

    [Fact]
    public void Document_NonApplicantOwned_StorageKeyChanged_NotNarrated()
    {
        var db = CreateDb();
        var document = new Document
        {
            FileName = "supporting.pdf",
            StorageKey = "old-key",
            ContentType = "application/pdf",
            SizeBytes = 100,
            Sha256 = new string('a', 64),
            DocumentType = DocumentType.SupportingDocument,
            ApplicationId = Guid.NewGuid(),
            UploadedAt = DateTimeOffset.UtcNow,
        };
        var entry = db.Attach(document);
        entry.Property(nameof(Document.StorageKey)).CurrentValue = "new-key";

        Assert.Null(ActivityNarrator.Narrate(entry, "Update"));
    }

    /// <summary>Isolates the <c>ApplicantId is null</c> condition specifically: unlike the test
    /// above (which changes the owner AND the document type together), this keeps DocumentType at
    /// a type that WOULD qualify for profile-document narration (CnicFront) and only changes the
    /// owner to application-scoped — confirming it's the missing ApplicantId, not the type, that
    /// suppresses narration.</summary>
    [Fact]
    public void Document_ApplicationOwnedCnicFront_StorageKeyChanged_NotNarrated()
    {
        var db = CreateDb();
        var document = new Document
        {
            FileName = "guarantor-cnic.png",
            StorageKey = "old-key",
            ContentType = "image/png",
            SizeBytes = 100,
            Sha256 = new string('a', 64),
            DocumentType = DocumentType.CnicFront,
            ApplicationId = Guid.NewGuid(), // application-scoped, not applicant-owned
            UploadedAt = DateTimeOffset.UtcNow,
        };
        var entry = db.Attach(document);
        entry.Property(nameof(Document.StorageKey)).CurrentValue = "new-key";

        Assert.Null(ActivityNarrator.Narrate(entry, "Update"));
    }

    [Fact]
    public void ApplicationUser_LastLoginAtOnlyChange_IsNoise()
    {
        var db = CreateDb();
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik" };
        var entry = Modify(db, user, (nameof(ApplicationUser.LastLoginAt), DateTimeOffset.UtcNow));

        // This is exactly what a login/refresh does to the user row — it must never reach
        // ActivityNarrator/BuildLog in the real interceptor pipeline.
        Assert.True(AuditNoiseFilter.IsNoiseOnlyChange(entry));
    }

    [Fact]
    public void ApplicationUser_ContextUpdate_BlanketFlagsEveryPropertyButNoneActuallyChanged_IsNoise()
    {
        // Reproduces the real bug this regression guards: ASP.NET Core Identity's EF UserStore
        // calls Context.Update(user) internally on every UserManager.UpdateAsync call (not just
        // Attach + mutate), which flags EVERY scalar property Modified regardless of whether its
        // value actually differs — a plain LastLoginAt bump on login/refresh would otherwise look
        // exactly like PasswordHash having changed too.
        var db = CreateDb();
        var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Ayesha Malik", PasswordHash = "unchanged-hash" };
        db.Attach(user);

        var entry = db.Update(user); // same object, same values — Update() still marks every property Modified

        // The noise filter is what actually stops this in production (it runs before BuildLog ever
        // calls Narrate) — assert that, rather than Narrate's own fallback, which always narrates
        // an ApplicationUser Update when asked and is never invoked for this case in practice.
        Assert.True(AuditNoiseFilter.IsNoiseOnlyChange(entry));
    }
}
