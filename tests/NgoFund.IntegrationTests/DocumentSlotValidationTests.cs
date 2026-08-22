using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Documents;
using static NgoFund.IntegrationTests.ApplicationCompletenessTestHelpers;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>
/// Proves the server-side <c>slotKey</c> validation added to document upload
/// (<see cref="NgoFund.Domain.Exceptions.DocumentSlotMismatchException"/>): a client-supplied
/// string that drives the completeness/Approved gate must be rejected, not trusted, when it names
/// an unrecognized slot, a document type the slot doesn't accept, or the wrong owner arm
/// (application vs. guarantor). Also proves <c>slot_key</c> round-trips through the documents-list
/// endpoint. Kept in its own class/fixture purely to stay under the shared login-rate-limit budget
/// — see <see cref="ApplicationCompletenessTests"/>.
/// </summary>
public class DocumentSlotValidationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Upload_BogusSlotKey_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50103-1111111-1");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], generalFundId);

        var response = await UploadAsync(client, application.Id, "CnicFront", "HOUSE_RENT.NOT_A_REAL_SLOT");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_DocumentTypeNotAcceptedByThatSlot_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50103-1111111-2");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], generalFundId);

        // MEMBERSHIP_CARD only accepts MembershipCard, not UtilityBill.
        var response = await UploadAsync(client, application.Id, "UtilityBill", "HOUSE_RENT.MEMBERSHIP_CARD");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WrongOwnerArmForSlotScope_IsRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50103-1111111-3");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await RozgarCompletenessTestHelpers.CreateRozgarApplicationAsync(client, applicant.Id, categories["ROZGAR"], generalFundId);

        // ROZGAR.GUARANTOR_CNIC is Guarantor-scoped — uploading it against the application itself must be rejected.
        var response = await UploadAsync(client, application.Id, "CnicFront", "ROZGAR.GUARANTOR_CNIC");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SlotKey_RoundTrips_ThroughGetByApplication()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var applicant = await CreateApplicantAsync(client, "50103-1111111-4");
        var (categories, generalFundId) = await LoadSeedIdsAsync(client);
        var application = await CreateBareApplicationAsync(client, applicant.Id, categories["HOUSE_RENT"], generalFundId);

        // HOUSE_RENT.APPLICANT_CNIC is Applicant-scoped (v1.4) so it can no longer be uploaded
        // against the application itself — RENT_RECEIPTS stays Application-scoped and exercises
        // the same round-trip.
        await UploadAsync(client, application.Id, "RentReceipt", "HOUSE_RENT.RENT_RECEIPTS");

        var docs = (await client.GetFromJsonAsync<List<DocumentDto>>($"/api/documents/by-application/{application.Id}"))!;

        Assert.Equal("HOUSE_RENT.RENT_RECEIPTS", Assert.Single(docs).SlotKey);
    }
}
