using NgoFund.Domain.Entities;
using NgoFund.Domain.Exceptions;

namespace NgoFund.UnitTests.Domain;

public class LoanAgreementTests
{
    private static FundCategory Fund(bool isZakat) => new() { Name = "Test Fund", IsZakat = isZakat };

    [Fact]
    public void EnsureFundIsRepayable_ZakatFund_Throws()
    {
        Assert.Throws<ZakatFundNotRepayableException>(() => LoanAgreement.EnsureFundIsRepayable(Fund(isZakat: true)));
    }

    [Fact]
    public void EnsureFundIsRepayable_GeneralFund_Succeeds()
    {
        LoanAgreement.EnsureFundIsRepayable(Fund(isZakat: false));
    }

    [Fact]
    public void EnsureCancellable_NothingRepaid_Succeeds()
    {
        var agreement = new LoanAgreement { LoanNumber = "LOAN-2026-00001" };

        agreement.EnsureCancellable(totalRepaid: 0m);
    }

    [Fact]
    public void EnsureCancellable_AnythingRepaid_Throws()
    {
        var agreement = new LoanAgreement { LoanNumber = "LOAN-2026-00001" };

        Assert.Throws<LoanAgreementLockedException>(() => agreement.EnsureCancellable(totalRepaid: 0.01m));
    }
}
