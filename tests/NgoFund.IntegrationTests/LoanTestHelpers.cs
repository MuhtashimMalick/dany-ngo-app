using System.Net;
using System.Net.Http.Json;
using NgoFund.Contracts.Loans;
using static NgoFund.IntegrationTests.PaymentTestHelpers;

namespace NgoFund.IntegrationTests;

/// <summary>Shared scaffolding for loan (qard al-hasan) integration tests, reusing <see cref="PaymentTestHelpers"/> for auth/funding/application setup instead of re-implementing it.</summary>
internal static class LoanTestHelpers
{
    public static async Task<LoanAgreementDto> CreateLoanAgreementAsync(
        HttpClient client, Guid applicationId, int installmentCount = 1, DateOnly? firstDueDate = null, string frequency = "Monthly")
    {
        var response = await client.PostAsJsonAsync("/api/loans", new CreateLoanAgreementRequest(
            applicationId, installmentCount, firstDueDate ?? DateOnly.FromDateTime(DateTime.UtcNow), frequency, null));
        return await ReadOrFailAsync<LoanAgreementDto>(response, HttpStatusCode.Created);
    }

    public static async Task<LoanRepaymentDto> RecordRepaymentAsync(
        HttpClient client, Guid loanAgreementId, decimal amount, string receivedFromName = "Repayer")
    {
        var response = await client.PostAsJsonAsync("/api/loans/repayments", new RecordLoanRepaymentRequest(
            loanAgreementId, amount, DateOnly.FromDateTime(DateTime.UtcNow), "Cash", null, null, receivedFromName, null, null));
        return await ReadOrFailAsync<LoanRepaymentDto>(response, HttpStatusCode.OK);
    }
}
