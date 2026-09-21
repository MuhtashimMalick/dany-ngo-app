using NgoFund.Domain.Applications;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.UnitTests.Domain;

public class FundApplicationTests
{
    private static ApplicationCategory Category(FundEligibility fundEligibility) => new()
    {
        Name = "Test Category",
        FundEligibility = fundEligibility,
    };

    private static FundCategory Fund(bool isZakat) => new()
    {
        Name = "Test Fund",
        IsZakat = isZakat,
    };

    // --- The Zakat rule (three-state): all 3 FundEligibility values x both fund types. ---

    [Fact]
    public void EnsureFundIsCompatible_ZakatOnlyCategory_AgainstZakatFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(FundEligibility.ZakatOnly), Fund(isZakat: true));
    }

    [Fact]
    public void EnsureFundIsCompatible_ZakatOnlyCategory_AgainstGeneralFund_Throws()
    {
        Assert.Throws<ZakatFundMismatchException>(() =>
            FundApplication.EnsureFundIsCompatible(Category(FundEligibility.ZakatOnly), Fund(isZakat: false)));
    }

    [Fact]
    public void EnsureFundIsCompatible_GeneralOnlyCategory_AgainstGeneralFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(FundEligibility.GeneralOnly), Fund(isZakat: false));
    }

    [Fact]
    public void EnsureFundIsCompatible_GeneralOnlyCategory_AgainstZakatFund_Throws()
    {
        Assert.Throws<ZakatFundMismatchException>(() =>
            FundApplication.EnsureFundIsCompatible(Category(FundEligibility.GeneralOnly), Fund(isZakat: true)));
    }

    [Fact]
    public void EnsureFundIsCompatible_EitherCategory_AgainstZakatFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(FundEligibility.Either), Fund(isZakat: true));
    }

    [Fact]
    public void EnsureFundIsCompatible_EitherCategory_AgainstGeneralFund_Succeeds()
    {
        FundApplication.EnsureFundIsCompatible(Category(FundEligibility.Either), Fund(isZakat: false));
    }

    // --- Guarantor gate (E2): categories with RequiresGuarantors > 0 (2 for ROZGAR) block Approved
    // until enough ApplicationGuarantor rows are on file. ---

    private static ApplicationCategory CategoryRequiringGuarantors(int requiresGuarantors) => new()
    {
        Name = "Rozgar/Business Help",
        FundEligibility = FundEligibility.GeneralOnly,
        RequiresGuarantors = requiresGuarantors,
    };

    [Fact]
    public void EnsureGuarantorsSatisfied_NoGuarantorsRequired_NeverThrows()
    {
        FundApplication.EnsureGuarantorsSatisfied("APP-0001", CategoryRequiringGuarantors(0), guarantorCount: 0);
    }

    [Fact]
    public void EnsureGuarantorsSatisfied_FewerThanRequired_Throws()
    {
        Assert.Throws<GuarantorsRequiredException>(() =>
            FundApplication.EnsureGuarantorsSatisfied("APP-0001", CategoryRequiringGuarantors(2), guarantorCount: 1));
    }

    [Fact]
    public void EnsureGuarantorsSatisfied_ExactlyRequired_Succeeds()
    {
        FundApplication.EnsureGuarantorsSatisfied("APP-0001", CategoryRequiringGuarantors(2), guarantorCount: 2);
    }

    [Fact]
    public void EnsureGuarantorsSatisfied_MoreThanRequired_Succeeds()
    {
        FundApplication.EnsureGuarantorsSatisfied("APP-0001", CategoryRequiringGuarantors(2), guarantorCount: 3);
    }

    // --- Guarantor-conflict gate (item 4, 2026-09 feedback) ---

    [Fact]
    public void EnsureGuarantorConflictsResolved_NoUnresolvedConflicts_NeverThrows()
    {
        FundApplication.EnsureGuarantorConflictsResolved("APP-0001", []);
    }

    [Fact]
    public void EnsureGuarantorConflictsResolved_UnresolvedConflictExists_Throws()
    {
        var ex = Assert.Throws<GuarantorConflictUnresolvedException>(() =>
            FundApplication.EnsureGuarantorConflictsResolved("APP-0001", [Guid.NewGuid()]));

        Assert.Contains("APP-0001", ex.Message);
    }

    // --- Mirror-direction gate: applicant is an active guarantor elsewhere ---

    [Fact]
    public void EnsureApplicantNotActiveGuarantorElsewhere_NoConflicts_NeverThrows()
    {
        FundApplication.EnsureApplicantNotActiveGuarantorElsewhere("APP-0001", [], overrideApproved: false);
    }

    [Fact]
    public void EnsureApplicantNotActiveGuarantorElsewhere_ConflictWithoutOverride_Throws()
    {
        var ex = Assert.Throws<ApplicantIsActiveGuarantorElsewhereException>(() =>
            FundApplication.EnsureApplicantNotActiveGuarantorElsewhere("APP-0001", ["APP-0002"], overrideApproved: false));

        Assert.Contains("APP-0001", ex.Message);
        Assert.Contains("APP-0002", ex.Message);
    }

    [Fact]
    public void EnsureApplicantNotActiveGuarantorElsewhere_ConflictWithApprovedOverride_NeverThrows()
    {
        FundApplication.EnsureApplicantNotActiveGuarantorElsewhere("APP-0001", ["APP-0002"], overrideApproved: true);
    }

    [Fact]
    public void HasValidApplicantGuarantorConflictOverride_CnicUnchangedSinceApproval_ReturnsTrue()
    {
        var application = new FundApplication
        {
            ApplicantGuarantorConflictOverrideApprovedAt = DateTimeOffset.UtcNow,
            ApplicantGuarantorConflictOverrideCnic = "42101-1111111-1",
        };

        Assert.True(application.HasValidApplicantGuarantorConflictOverride("42101-1111111-1"));
    }

    [Fact]
    public void HasValidApplicantGuarantorConflictOverride_CnicChangedSinceApproval_ReturnsFalse()
    {
        var application = new FundApplication
        {
            ApplicantGuarantorConflictOverrideApprovedAt = DateTimeOffset.UtcNow,
            ApplicantGuarantorConflictOverrideCnic = "42101-1111111-1",
        };

        Assert.False(application.HasValidApplicantGuarantorConflictOverride("42101-2222222-2"));
    }

    // --- The completeness gate: EnsureApplicationIsComplete throws when the pre-computed
    // ApplicationCompletenessResult.IsComplete is false, mirroring EnsureGuarantorsSatisfied's shape. ---

    private static readonly RequiredField DummyField = new("Dummy", "Dummy Field", "Section");

    [Fact]
    public void EnsureApplicationIsComplete_ResultIsComplete_NeverThrows()
    {
        var result = new ApplicationCompletenessResult(IsComplete: true, MissingFields: [], Slots: []);

        FundApplication.EnsureApplicationIsComplete("APP-0001", result);
    }

    [Fact]
    public void EnsureApplicationIsComplete_ResultIncomplete_ThrowsWithMissingFieldLabel()
    {
        var result = new ApplicationCompletenessResult(IsComplete: false, MissingFields: [DummyField], Slots: []);

        var ex = Assert.Throws<ApplicationIncompleteException>(() => FundApplication.EnsureApplicationIsComplete("APP-0001", result));

        Assert.Contains("Dummy Field", ex.MissingFieldLabels);
        Assert.Empty(ex.MissingDocumentLabels);
    }

    [Fact]
    public void EnsureApplicationIsComplete_UnsatisfiedRequiredSlot_ThrowsWithMissingDocumentLabel()
    {
        var slot = new RequiredDocumentSlot("TEST.SLOT", "Test Document", true, 1, DocumentSlotOwnerScope.Application, [DocumentType.CnicFront]);
        var status = new DocumentSlotStatus(slot, null, null, null, [], IsSatisfied: false);
        var result = new ApplicationCompletenessResult(IsComplete: false, MissingFields: [], Slots: [status]);

        var ex = Assert.Throws<ApplicationIncompleteException>(() => FundApplication.EnsureApplicationIsComplete("APP-0001", result));

        Assert.Contains("Test Document", ex.MissingDocumentLabels);
        Assert.Empty(ex.MissingFieldLabels);
    }

    [Fact]
    public void EnsureApplicationIsComplete_UnsatisfiedOptionalSlot_IsNeverListedAsMissing()
    {
        var slot = new RequiredDocumentSlot("TEST.OPTIONAL", "Optional Document", false, 1, DocumentSlotOwnerScope.Application, [DocumentType.CnicFront]);
        var status = new DocumentSlotStatus(slot, null, null, null, [], IsSatisfied: false);
        // IsComplete is computed by the evaluator (an unsatisfied optional slot never flips it to
        // false) — this test exercises EnsureApplicationIsComplete's own filtering independently by
        // forcing IsComplete=false via an unrelated missing field, and checking the optional slot
        // still never appears in the resulting exception's document list.
        var result = new ApplicationCompletenessResult(IsComplete: false, MissingFields: [DummyField], Slots: [status]);

        var ex = Assert.Throws<ApplicationIncompleteException>(() => FundApplication.EnsureApplicationIsComplete("APP-0001", result));

        Assert.DoesNotContain("Optional Document", ex.MissingDocumentLabels);
    }

    [Fact]
    public void EnsureApplicationIsComplete_GuarantorScopedSlot_UsesDisambiguatedDisplayLabel()
    {
        var slot = new RequiredDocumentSlot("ROZGAR.GUARANTOR_CNIC", "Guarantor's CNIC", true, 1, DocumentSlotOwnerScope.Guarantor, [DocumentType.CnicFront]);
        var status = new DocumentSlotStatus(slot, Guid.NewGuid(), GuarantorSequenceNumber: 2, "Guarantor Two", [], IsSatisfied: false);
        var result = new ApplicationCompletenessResult(IsComplete: false, MissingFields: [], Slots: [status]);

        var ex = Assert.Throws<ApplicationIncompleteException>(() => FundApplication.EnsureApplicationIsComplete("APP-0001", result));

        Assert.Contains("Guarantor's CNIC (Guarantor 2)", ex.MissingDocumentLabels);
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

    // --- ApplicationStatusRules.ActiveStatuses must match AllowedTransitions' terminal states
    // exactly (the set of statuses with zero outgoing transitions, regardless of payment total) —
    // the duplicate-application badge and the guarantor-conflict gate both read from that one set. ---

    [Fact]
    public void ActiveStatuses_MatchesNonTerminalStatusesFromAllowedTransitions()
    {
        var terminal = Enum.GetValues<ApplicationStatus>()
            .Where(s => FundApplication.GetAllowedNextStatuses(s, totalCompletedPaid: 0m).Count == 0
                && FundApplication.GetAllowedNextStatuses(s, totalCompletedPaid: 5000m).Count == 0)
            .ToHashSet();

        var expectedActive = Enum.GetValues<ApplicationStatus>().Except(terminal).ToHashSet();

        Assert.Equal(expectedActive, ApplicationStatusRules.ActiveStatuses.ToHashSet());
    }

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
