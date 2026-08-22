using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NgoFund.Contracts.Applications;
using NgoFund.Contracts.Documents;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Shared scaffolding for satisfying the full ROZGAR completeness manifest (every required field
/// plus every required application/guarantor document slot — see
/// <c>NgoFund.Domain.Applications.ApplicationRequirements</c>). Used by
/// <see cref="CategoryApplicationDetailsTests"/> (which needs a genuinely-complete application to
/// prove the guarantor-count gate in isolation, now that the completeness gate also runs on
/// Approved) and <see cref="ApplicationCompletenessTests"/>.
/// </summary>
internal static class RozgarCompletenessTestHelpers
{
    public static readonly UpsertBusinessLoanApplicationDetailsRequest FullyValidBusinessLoanDetails = new(
        PaperFormNumber: null, BusinessPhone: null, Education: null, Skill: "Tailoring", Experience: "5 years",
        OtherIncomeSources: null, TotalMonthlyExpenses: 20000m, ProposedBusinessDescription: "Tailoring shop",
        ProposedBusinessLocation: "Main Bazaar", CapitalRequired: 50000m, CapitalAlreadyAvailable: 10000m,
        HasPriorBusinessExperience: false, PriorBusinessDetails: null,
        EmergencyContactName: "Emergency Contact", EmergencyContactCnic: null, EmergencyContactPhone: "0300-0000000");

    /// <summary>Base (FundApplication-level) fields ROZGAR needs, as <c>CreateApplicationRequest</c> optional-arg overrides.</summary>
    public static async Task<ApplicationDto> CreateRozgarApplicationAsync(HttpClient client, Guid applicantId, Guid categoryId, Guid fundId, decimal amount = 10000m)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            applicantId, categoryId, fundId, amount, "Normal", DateOnly.FromDateTime(DateTime.UtcNow), "Test",
            DeclaredBusinessAddress: "123 Business Street",
            DeclarationAcceptedAt: DateTimeOffset.UtcNow,
            TermsAcceptedAt: DateTimeOffset.UtcNow));
        return await ReadOrFailAsync<ApplicationDto>(response, HttpStatusCode.Created);
    }

    /// <summary>Uploads one document satisfying every required application-scope ROZGAR slot
    /// (PASSPORT_PHOTOS needs two). ROZGAR.APPLICANT_CNIC and ROZGAR.MEMBERSHIP_CARD are NOT
    /// uploaded here (v1.4, B2/B6): they're Applicant-scoped now, satisfied by the CNIC/membership-
    /// card documents every test applicant already has from <see cref="ApplicationCompletenessTestHelpers.CreateApplicantAsync"/>
    /// — an application-owned upload against either slot key would be rejected as an owner-scope
    /// mismatch by DocumentService.EnsureSlotMatchesAsync.</summary>
    public static async Task UploadAllRequiredApplicationDocumentsAsync(HttpClient client, Guid applicationId)
    {
        await UploadAsync(client, applicationId, null, "SignedApplicationForm", "ROZGAR.LOAN_APPLICATION");
        await UploadAsync(client, applicationId, null, "BusinessPlan", "ROZGAR.BUSINESS_DETAILS");
        await UploadAsync(client, applicationId, null, "FormB", "ROZGAR.FORM_B");
        await UploadAsync(client, applicationId, null, "PassportPhoto", "ROZGAR.PASSPORT_PHOTOS");
        await UploadAsync(client, applicationId, null, "PassportPhoto", "ROZGAR.PASSPORT_PHOTOS");
        await UploadAsync(client, applicationId, null, "UtilityBill", "ROZGAR.UTILITY_BILLS");
    }

    public static async Task UploadRequiredGuarantorDocumentsAsync(HttpClient client, Guid guarantorId)
    {
        await UploadAsync(client, null, guarantorId, "CnicFront", "ROZGAR.GUARANTOR_CNIC");
        await UploadAsync(client, null, guarantorId, "MembershipCard", "ROZGAR.GUARANTOR_MEMBERSHIP_CARD");
    }

    public static async Task<DocumentDto> UploadAsync(HttpClient client, Guid? applicationId, Guid? guarantorId, string documentType, string? slotKey)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TestFiles.MinimalJpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "doc.jpg");

        var query = $"documentType={documentType}";
        if (slotKey is not null) query += $"&slotKey={slotKey}";
        if (applicationId is not null) query += $"&applicationId={applicationId}";
        if (guarantorId is not null) query += $"&applicationGuarantorId={guarantorId}";

        var response = await client.PostAsync($"/api/documents?{query}", form);
        return await ReadOrFailAsync<DocumentDto>(response, HttpStatusCode.OK);
    }
}
