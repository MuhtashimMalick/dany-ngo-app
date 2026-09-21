using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NgoFund.Contracts.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Persistence;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// v1.4 (B): the applicant's own CNIC-front/back and Jamaat membership-card documents (mandatory at
/// applicant creation, see <see cref="ApplicantCreationDocumentTests"/>) now satisfy the equivalent
/// per-application APPLICANT_CNIC/MEMBERSHIP_CARD slots on HOUSE_RENT/SHAADI/ROZGAR, so staff don't
/// re-upload them per application. Covers the happy path across all three categories, the trap
/// guarded against in <c>ApplicationRequirements</c> (the applicant's own membership card must never
/// satisfy the BRIDE's membership-card slot), the legacy application-owned fallback for documents
/// uploaded before this change, and that an applicant-satisfied slot doesn't let unrelated required
/// slots pass. Kept in its own class/fixture purely to stay under the shared login-rate-limit budget
/// — see <see cref="ApplicationCompletenessTests"/>.
/// </summary>
public class ApplicantProfileDocumentCompletenessTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Theory]
    [InlineData("HOUSE_RENT", "HOUSE_RENT.APPLICANT_CNIC", "HOUSE_RENT.MEMBERSHIP_CARD")]
    [InlineData("SHAADI", "SHAADI.APPLICANT_CNIC", "SHAADI.APPLICANT_MEMBERSHIP_CARD")]
    [InlineData("ROZGAR", "ROZGAR.APPLICANT_CNIC", "ROZGAR.MEMBERSHIP_CARD")]
    public async Task ApplicantWithProfileDocuments_SatisfiesApplicantCnicAndMembershipCardSlots_WithNoApplicationLevelUpload(
        string categoryCode, string cnicSlotKey, string membershipSlotKey)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        // CreateApplicantAsync uploads CnicFront, CnicBack, and MembershipCard onto the applicant's
        // own profile (A3/v1.4) — nothing is uploaded onto the application itself below.
        var applicant = await CreateApplicantAsync(client, $"50104-{Math.Abs(categoryCode.GetHashCode()) % 10000000:D7}-1");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        // ROZGAR is GeneralOnly; HOUSE_RENT/SHAADI are ZakatOnly under the v1.5 FundEligibility
        // amendment (they used to be dual-eligible) — pick whichever fund this category accepts.
        var fundId = categoryCode == "ROZGAR" ? generalFundId : await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories[categoryCode], fundId);

        var completeness = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);

        var cnicSlot = completeness.Slots.Single(s => s.SlotKey == cnicSlotKey);
        var membershipSlot = completeness.Slots.Single(s => s.SlotKey == membershipSlotKey);

        Assert.True(cnicSlot.IsSatisfied);
        Assert.True(cnicSlot.SatisfiedByApplicantProfile);
        Assert.NotEmpty(cnicSlot.Documents); // the applicant's own CnicFront/CnicBack, matched by type

        Assert.True(membershipSlot.IsSatisfied);
        Assert.True(membershipSlot.SatisfiedByApplicantProfile);
        Assert.NotEmpty(membershipSlot.Documents);
    }

    /// <summary>
    /// THE regression guard: SHAADI.BRIDE_MEMBERSHIP_CARD stays Application-scoped on purpose (it's
    /// the BRIDE's document, not the applicant's) — if that guard is ever accidentally widened to
    /// Applicant scope, this test catches it, because the applicant here has a membership card on
    /// file but the bride's slot must still read unsatisfied.
    /// </summary>
    [Fact]
    public async Task Shaadi_ApplicantHasMembershipCardOnFile_BrideMembershipCardSlotStillUnsatisfied()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50104-2222222-2");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["SHAADI"], zakatFundId);

        var completeness = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);

        var brideMembershipSlot = completeness.Slots.Single(s => s.SlotKey == "SHAADI.BRIDE_MEMBERSHIP_CARD");
        Assert.False(brideMembershipSlot.IsSatisfied);
        Assert.False(brideMembershipSlot.SatisfiedByApplicantProfile);

        // The applicant-scoped slots on the SAME application, meanwhile, ARE satisfied — proving
        // the applicant's membership card was matched narrowly, not indiscriminately.
        Assert.True(completeness.Slots.Single(s => s.SlotKey == "SHAADI.APPLICANT_MEMBERSHIP_CARD").IsSatisfied);
    }

    /// <summary>
    /// Legacy fallback: an application created before v1.4, with its CNIC uploaded as an
    /// application-owned document under the old slot key, must keep reading as satisfied even
    /// though the applicant record itself has zero documents. Built via direct DB access (bypassing
    /// the API's applicant-creation validator, which now requires profile documents — see A3) to
    /// reproduce that pre-v1.4 shape.
    /// </summary>
    [Fact]
    public async Task LegacyApplicationOwnedCnic_UnderOldSlotKey_StillReportsSatisfied_EvenWithNoApplicantDocuments()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);

        Guid applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var applicant = new Applicant { Cnic = "50104-3333333-3", FullName = "Legacy Applicant", Gender = Gender.Male };
            db.Applicants.Add(applicant);
            await db.SaveChangesAsync();

            var application = new FundApplication
            {
                ApplicationNumber = "LEGACY-0001",
                ApplicantId = applicant.Id,
                ApplicationCategoryId = categories["HOUSE_RENT"],
                FundCategoryId = zakatFundId,
                RequestedAmount = 10000m,
                ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            };
            db.Applications.Add(application);
            await db.SaveChangesAsync();

            db.Documents.Add(new Document
            {
                FileName = "cnic.jpg",
                StorageKey = Guid.NewGuid().ToString(),
                ContentType = "image/jpeg",
                SizeBytes = 10,
                Sha256 = new string('a', 64),
                DocumentType = DocumentType.CnicFront,
                ApplicationId = application.Id,
                SlotKey = "HOUSE_RENT.APPLICANT_CNIC", // pre-v1.4 shape: application-owned, old slot key
                UploadedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            applicationId = application.Id;
        }

        var completeness = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{applicationId}/completeness"), HttpStatusCode.OK);

        var cnicSlot = completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.APPLICANT_CNIC");
        Assert.True(cnicSlot.IsSatisfied);
        Assert.False(cnicSlot.SatisfiedByApplicantProfile); // satisfied via the legacy fallback, not the applicant's profile
        Assert.Single(cnicSlot.Documents);
    }

    /// <summary>
    /// An applicant-satisfied slot only satisfies ITSELF — it must never let an unrelated slot pass
    /// for free. HOUSE_RENT.UTILITY_BILLS is optional (item 6, 2026-09 feedback: MinCount 0) so it
    /// always reads as satisfied regardless; overall completeness still correctly reports incomplete
    /// because the housing-details fields (ApplicantAge etc.) were never filled in.
    /// </summary>
    [Fact]
    public async Task ApplicantSatisfiedSlots_DoNotSatisfyUnrelatedApplicationScopedSlots()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50104-4444444-4");
        var (categories, _) = await LoadSeedIdsAsync(client);
        var zakatFundId = await LoadZakatFundIdAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], zakatFundId);

        var completeness = await ReadOrFailAsync<ApplicationCompletenessDto>(
            await client.GetAsync($"/api/applications/{application.Id}/completeness"), HttpStatusCode.OK);

        Assert.True(completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.APPLICANT_CNIC").IsSatisfied);
        Assert.True(completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.MEMBERSHIP_CARD").IsSatisfied);
        Assert.True(completeness.Slots.Single(s => s.SlotKey == "HOUSE_RENT.UTILITY_BILLS").IsSatisfied);
        Assert.False(completeness.IsComplete);
    }
}
