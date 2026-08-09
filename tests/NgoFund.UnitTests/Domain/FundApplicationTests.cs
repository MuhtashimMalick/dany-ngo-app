using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.UnitTests.Domain;

public class FundApplicationTests
{
    private static ApplicationCategory Category(bool isZakatEligible) => new()
    {
        Name = "Test Category",
        IsZakatEligible = isZakatEligible,
    };

    private static FundCategory Fund(bool isZakat) => new()
    {
        Name = "Test Fund",
        IsZakat = isZakat,
    };

    [Fact]
    public void EnsureFundIsCompatible_ZakatEligibleCategory_AgainstZakatFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(isZakatEligible: true), Fund(isZakat: true));
    }

    [Fact]
    public void EnsureFundIsCompatible_ZakatEligibleCategory_AgainstGeneralFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(isZakatEligible: true), Fund(isZakat: false));
    }

    [Fact]
    public void EnsureFundIsCompatible_NonZakatEligibleCategory_AgainstGeneralFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(isZakatEligible: false), Fund(isZakat: false));
    }

    [Fact]
    public void EnsureFundIsCompatible_NonZakatEligibleCategory_AgainstZakatFund_Throws()
    {
        Assert.Throws<ZakatFundMismatchException>(() =>
            FundApplication.EnsureFundIsCompatible(Category(isZakatEligible: false), Fund(isZakat: true)));
    }

    [Theory]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.OnHold, ApplicationStatus.Approved)]
    public void TransitionTo_AllowedTransition_Succeeds(ApplicationStatus from, ApplicationStatus to)
    {
        var application = new FundApplication { Status = from };

        application.TransitionTo(to, totalCompletedPaid: 0m);

        Assert.Equal(to, application.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Paid, ApplicationStatus.Pending)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Paid)]
    // These two used to be asserted as ALLOWED — that was exactly the bug (a reviewer could mark
    // an application Paid/PartiallyPaid by hand regardless of actual payments). They must now throw.
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.PartiallyPaid)]
    [InlineData(ApplicationStatus.PartiallyPaid, ApplicationStatus.Paid)]
    public void TransitionTo_DisallowedTransition_Throws(ApplicationStatus from, ApplicationStatus to)
    {
        var application = new FundApplication { Status = from };

        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(to, totalCompletedPaid: 0m));
    }

    [Fact]
    public void TransitionTo_SameStatus_IsANoOp()
    {
        var application = new FundApplication { Status = ApplicationStatus.Pending };

        application.TransitionTo(ApplicationStatus.Pending, totalCompletedPaid: 0m);

        Assert.Equal(ApplicationStatus.Pending, application.Status);
    }

    // --- Regression: Paid/PartiallyPaid must never be a manual target, from any status, paid or not. ---

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void GetAllowedNextStatuses_NeverIncludesPaidOrPartiallyPaid_RegardlessOfPaymentTotal(ApplicationStatus current)
    {
        var atZero = FundApplication.GetAllowedNextStatuses(current, totalCompletedPaid: 0m);
        var atNonZero = FundApplication.GetAllowedNextStatuses(current, totalCompletedPaid: 5000m);

        Assert.DoesNotContain(ApplicationStatus.Paid, atZero);
        Assert.DoesNotContain(ApplicationStatus.PartiallyPaid, atZero);
        Assert.DoesNotContain(ApplicationStatus.Paid, atNonZero);
        Assert.DoesNotContain(ApplicationStatus.PartiallyPaid, atNonZero);
    }

    // Paid -> Paid and PartiallyPaid -> PartiallyPaid are excluded: TransitionTo treats same-status
    // as a pre-existing, unrelated no-op (see TransitionTo_SameStatus_IsANoOp) rather than a
    // rejected transition, so they're not part of this regression.
    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void TransitionTo_Paid_AlwaysThrows(ApplicationStatus from)
    {
        if (from == ApplicationStatus.Paid)
        {
            return;
        }

        var application = new FundApplication { Status = from };

        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(ApplicationStatus.Paid, totalCompletedPaid: 0m));
        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(ApplicationStatus.Paid, totalCompletedPaid: 5000m));
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void TransitionTo_PartiallyPaid_AlwaysThrows(ApplicationStatus from)
    {
        if (from == ApplicationStatus.PartiallyPaid)
        {
            return;
        }

        var application = new FundApplication { Status = from };

        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(ApplicationStatus.PartiallyPaid, totalCompletedPaid: 0m));
        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(ApplicationStatus.PartiallyPaid, totalCompletedPaid: 5000m));
    }

    public static TheoryData<ApplicationStatus> AllStatuses() =>
        new(Enum.GetValues<ApplicationStatus>());

    // --- Regression (N2): once money has moved, Rejected/Pending/UnderReview must be unreachable,
    // including via OnHold (the laundering path: PartiallyPaid -> OnHold -> Rejected). ---

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.PartiallyPaid)]
    [InlineData(ApplicationStatus.OnHold)]
    public void GetAllowedNextStatuses_WithPayments_ExcludesRejectedPendingUnderReview(ApplicationStatus current)
    {
        var allowed = FundApplication.GetAllowedNextStatuses(current, totalCompletedPaid: 1000m);

        Assert.DoesNotContain(ApplicationStatus.Rejected, allowed);
        Assert.DoesNotContain(ApplicationStatus.Pending, allowed);
        Assert.DoesNotContain(ApplicationStatus.UnderReview, allowed);
    }

    [Fact]
    public void TransitionTo_OnHoldToRejected_WithPaymentsMade_Throws()
    {
        var application = new FundApplication { Status = ApplicationStatus.OnHold };

        Assert.Throws<InvalidStatusTransitionException>(() => application.TransitionTo(ApplicationStatus.Rejected, totalCompletedPaid: 1000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void TransitionTo_OnHoldToApproved_IsLegalRegardlessOfPaymentTotal(decimal totalCompletedPaid)
    {
        var application = new FundApplication { Status = ApplicationStatus.OnHold };

        application.TransitionTo(ApplicationStatus.Approved, totalCompletedPaid);

        Assert.Equal(ApplicationStatus.Approved, application.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Approved, 0, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Approved, 5000, ApplicationStatus.PartiallyPaid)]
    [InlineData(ApplicationStatus.PartiallyPaid, 10000, ApplicationStatus.Paid)]
    [InlineData(ApplicationStatus.Paid, 6000, ApplicationStatus.PartiallyPaid)]
    [InlineData(ApplicationStatus.Paid, 0, ApplicationStatus.Approved)]
    public void RecomputeStatusFromPayments_InPaymentLifecycle_SetsExpectedStatus(
        ApplicationStatus from, decimal totalCompletedPaid, ApplicationStatus expected)
    {
        var application = new FundApplication { Status = from, ApprovedAmount = 10000m };

        application.RecomputeStatusFromPayments(totalCompletedPaid);

        Assert.Equal(expected, application.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.OnHold)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Pending)]
    [InlineData(ApplicationStatus.UnderReview)]
    public void RecomputeStatusFromPayments_OutsidePaymentLifecycle_LeavesStatusUnchanged(ApplicationStatus status)
    {
        var application = new FundApplication { Status = status, ApprovedAmount = 10000m };

        application.RecomputeStatusFromPayments(5000m);

        Assert.Equal(status, application.Status);
    }

    // --- Regression (N1): RecomputeStatusFromPayments must report the previous status on a real
    // change, and null on a no-op, so the caller knows whether to journal a history row. ---

    [Fact]
    public void RecomputeStatusFromPayments_StatusChanges_ReturnsPreviousStatus()
    {
        var application = new FundApplication { Status = ApplicationStatus.Approved, ApprovedAmount = 10000m };

        var previous = application.RecomputeStatusFromPayments(5000m);

        Assert.Equal(ApplicationStatus.Approved, previous);
        Assert.Equal(ApplicationStatus.PartiallyPaid, application.Status);
    }

    [Fact]
    public void RecomputeStatusFromPayments_StatusUnchanged_ReturnsNull()
    {
        var application = new FundApplication { Status = ApplicationStatus.PartiallyPaid, ApprovedAmount = 10000m };

        var previous = application.RecomputeStatusFromPayments(5000m);

        Assert.Null(previous);
        Assert.Equal(ApplicationStatus.PartiallyPaid, application.Status);
    }
}
