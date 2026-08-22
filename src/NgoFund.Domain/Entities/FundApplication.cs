using NgoFund.Domain.Applications;
using NgoFund.Domain.Common;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Domain.Entities;

/// <summary>
/// A needy person's request for assistance under one <see cref="ApplicationCategory"/>, drawing
/// from one <see cref="FundCategory"/>. Named <c>FundApplication</c> rather than "Application" to
/// avoid colliding with the <c>NgoFund.Application</c> project/namespace elsewhere in the
/// solution. Financial/workflow record — never soft-deleted; its <see cref="Status"/> carries
/// the "removed" semantics (Rejected/OnHold) instead, preserving history.
/// </summary>
public class FundApplication : BaseEntity, IUpdateAuditable
{
    public string ApplicationNumber { get; set; } = null!;

    public Guid ApplicantId { get; set; }
    public Applicant Applicant { get; set; } = null!;

    public Guid ApplicationCategoryId { get; set; }
    public ApplicationCategory ApplicationCategory { get; set; } = null!;

    /// <summary>Which fund this application draws from. Must satisfy the Zakat rule — see <see cref="EnsureFundIsCompatible"/>.</summary>
    public Guid FundCategoryId { get; set; }
    public FundCategory FundCategory { get; set; } = null!;

    public decimal RequestedAmount { get; set; }

    public decimal? ApprovedAmount { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    public ApplicationPriority Priority { get; set; } = ApplicationPriority.Normal;

    public DateOnly ApplicationDate { get; set; }

    public string? Purpose { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    // --- Point-in-time snapshots, as stated on THIS application ---
    //
    // These `Declared*` fields intentionally duplicate shape with fields on `Applicant`
    // (`Applicant.MonthlyIncome`, `Applicant.HouseholdSize`, `Applicant.Address`, ...). That is
    // deliberate, not a DRY violation to "fix":
    //   - `Applicant.MonthlyIncome`/`HouseholdSize`/etc. are "latest known" person-level facts,
    //     maintained on the Applicants screen and updated whenever staff learn something new.
    //   - `FundApplication.Declared*` are a snapshot of what the applicant stated on THIS specific
    //     application at THIS point in time (often re-keyed verbatim from a Google Form/paper
    //     form submission).
    // The two must NEVER be kept in sync automatically: an applicant's income can change between
    // two applications filed a year apart, and re-keying a form must preserve exactly what was
    // declared then, not silently overwrite it with whatever the Applicant record says today (or
    // vice versa). Any future code that's tempted to add a sync job between these two should not.
    public decimal? DeclaredMonthlyIncome { get; set; }
    public int? DeclaredHouseholdSize { get; set; }
    public int? DeclaredEarningMembers { get; set; }
    public string? DeclaredResidentialAddress { get; set; }
    public string? DeclaredBusinessAddress { get; set; }
    public HouseStatus? DeclaredHouseStatus { get; set; }

    // --- Intake / re-keying provenance ---
    public ApplicationIntakeChannel IntakeChannel { get; set; } = ApplicationIntakeChannel.InApp;

    /// <summary>Google Form response ID / paper form number this application was re-keyed from, if any. Unique when present.</summary>
    public string? ExternalFormReference { get; set; }

    /// <summary>When the applicant originally submitted (Google Form/paper) — as opposed to <see cref="BaseEntity.CreatedAt"/>, which is when staff keyed it into this system.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? DeclarationAcceptedAt { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public string? TermsVersion { get; set; }

    public HousingApplicationDetails? HousingDetails { get; set; }
    public MarriageApplicationDetails? MarriageDetails { get; set; }
    public BusinessLoanApplicationDetails? BusinessLoanDetails { get; set; }
    public ICollection<ApplicationGuarantor> Guarantors { get; set; } = [];

    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<ApplicationRemark> Remarks { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// The Zakat rule: a non-Zakat-eligible application category may only be funded from a
    /// non-Zakat (General) fund. Zakat-eligible categories may draw from either fund. Call before
    /// assigning/changing <see cref="FundCategoryId"/>. Mirrored by the
    /// <c>fn_enforce_zakat_eligibility</c> DB trigger as defence in depth.
    /// </summary>
    public static void EnsureFundIsCompatible(ApplicationCategory category, FundCategory fund)
    {
        if (fund.IsZakat && !category.IsZakatEligible)
        {
            throw new ZakatFundMismatchException(category.Name, fund.Name);
        }
    }

    /// <summary>
    /// Blocks a category change once category-scoped data already exists for this application: the
    /// current category's details row (housing/marriage/business-loan), any
    /// <see cref="ApplicationGuarantor"/> rows, or any <see cref="Document"/> saved into a slot. Call
    /// only when the category is actually changing (the caller already knows this — it's the one
    /// comparing old vs. new category IDs). See <see cref="ApplicationCategoryChangeBlockedException"/>.
    /// </summary>
    public static void EnsureCategoryChangeAllowed(string applicationNumber, string currentCategoryName, bool hasCategoryScopedData)
    {
        if (hasCategoryScopedData)
        {
            throw new ApplicationCategoryChangeBlockedException(applicationNumber, currentCategoryName);
        }
    }

    /// <summary>
    /// The guarantor gate: a category with <see cref="ApplicationCategory.RequiresGuarantors"/> &gt; 0
    /// (2 for ROZGAR, 0 elsewhere) needs at least that many <see cref="ApplicationGuarantor"/> rows
    /// on file before the application can be transitioned to <see cref="ApplicationStatus.Approved"/>.
    /// Called from the same site as <see cref="EnsureFundIsCompatible"/> — the Approved-transition
    /// call path in <c>FundApplicationService.ChangeStatusAsync</c> — deliberately NOT folded into
    /// <see cref="TransitionTo"/> itself, which knows nothing about categories or guarantor counts.
    /// </summary>
    public static void EnsureGuarantorsSatisfied(string applicationNumber, ApplicationCategory category, int guarantorCount)
    {
        if (guarantorCount < category.RequiresGuarantors)
        {
            throw new GuarantorsRequiredException(applicationNumber, category.RequiresGuarantors, guarantorCount);
        }
    }

    /// <summary>
    /// The completeness gate: closes the bug where an application could reach
    /// <see cref="ApplicationStatus.Approved"/> with zero category-specific details and zero
    /// required documents on file. Called from the same Approved-transition call site as
    /// <see cref="EnsureGuarantorsSatisfied"/>, right after it, with a result already computed by
    /// <see cref="ApplicationCompletenessEvaluator.Evaluate"/> — this method just turns a
    /// negative verdict into an exception.
    /// </summary>
    public static void EnsureApplicationIsComplete(string applicationNumber, ApplicationCompletenessResult result)
    {
        if (!result.IsComplete)
        {
            throw new ApplicationIncompleteException(
                applicationNumber,
                result.MissingFields.Select(f => f.Label).ToList(),
                result.Slots.Where(s => s.Slot.IsRequired && !s.IsSatisfied).Select(s => s.DisplayLabel).ToList());
        }
    }

    /// <summary>
    /// Manually-selectable transitions (reviewer-driven, via the status-change UI/endpoint).
    /// Deliberately excludes <see cref="ApplicationStatus.Paid"/> and
    /// <see cref="ApplicationStatus.PartiallyPaid"/> as targets from every source status — those
    /// two are reachable ONLY through <see cref="RecomputeStatusFromPayments"/>, which derives
    /// them from actual completed payments. Listing them here as human-selectable targets is
    /// exactly the bug this table was rewritten to fix (a reviewer could mark an application Paid
    /// with zero, or less than, money actually disbursed).
    /// </summary>
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> AllowedTransitions = new()
    {
        [ApplicationStatus.Pending] = [ApplicationStatus.UnderReview, ApplicationStatus.Rejected, ApplicationStatus.OnHold],
        [ApplicationStatus.UnderReview] = [ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.OnHold, ApplicationStatus.Pending],
        [ApplicationStatus.OnHold] = [ApplicationStatus.Pending, ApplicationStatus.UnderReview, ApplicationStatus.Approved, ApplicationStatus.Rejected],
        [ApplicationStatus.Approved] = [ApplicationStatus.OnHold, ApplicationStatus.Rejected],
        [ApplicationStatus.PartiallyPaid] = [ApplicationStatus.OnHold],
        [ApplicationStatus.Paid] = [],
        [ApplicationStatus.Rejected] = [],
    };

    /// <summary>
    /// Statuses pruned from the base table above once any money has actually moved
    /// (<paramref name="totalCompletedPaid"/> &gt; 0 in <see cref="GetAllowedNextStatuses"/>).
    /// Closes the laundering path where a <see cref="ApplicationStatus.PartiallyPaid"/>
    /// application — money already disbursed — could be walked to terminal
    /// <see cref="ApplicationStatus.Rejected"/> via <c>PartiallyPaid → OnHold → Rejected</c>: once
    /// paid, <see cref="ApplicationStatus.OnHold"/> can only go back to
    /// <see cref="ApplicationStatus.Approved"/> (which immediately re-settles via
    /// <see cref="RecomputeStatusFromPayments"/>) or stay put.
    /// </summary>
    private static readonly ApplicationStatus[] BlockedOncePaid =
        [ApplicationStatus.Pending, ApplicationStatus.UnderReview, ApplicationStatus.Rejected];

    /// <summary>
    /// Validates a status change against <see cref="GetAllowedNextStatuses"/> — the same
    /// payment-aware set of legal manual targets the UI dropdown is built from, so this can never
    /// accept a transition the UI wasn't allowed to offer. <see cref="ApplicationStatus.Paid"/> and
    /// <see cref="ApplicationStatus.PartiallyPaid"/> are never legal manual targets;
    /// <see cref="ApplicationStatus.Rejected"/> stops being reachable once any payment has been
    /// completed, even via <see cref="ApplicationStatus.OnHold"/>. <see cref="ApplicationStatus.Rejected"/>
    /// and <see cref="ApplicationStatus.Paid"/> themselves remain terminal (no outgoing transitions at all).
    /// </summary>
    public void TransitionTo(ApplicationStatus newStatus, decimal totalCompletedPaid)
    {
        if (newStatus == Status)
        {
            return;
        }

        if (!GetAllowedNextStatuses(Status, totalCompletedPaid).Contains(newStatus))
        {
            throw new InvalidStatusTransitionException(Status, newStatus);
        }

        Status = newStatus;
    }

    /// <summary>
    /// The statuses <paramref name="current"/> may legally transition to manually — the single
    /// source of truth for the workflow, exposed so callers (e.g. the Desktop status-change UI, via
    /// <c>ApplicationDto.AllowedNextStatuses</c>) can offer only legal choices instead of duplicating
    /// this table. Starts from <see cref="AllowedTransitions"/>, then, once
    /// <paramref name="totalCompletedPaid"/> is greater than zero, removes <see cref="BlockedOncePaid"/>
    /// (see its doc comment for why) — the one overlay both this method and <see cref="TransitionTo"/>
    /// share, so the rule lives in exactly one place.
    /// </summary>
    public static IReadOnlyList<ApplicationStatus> GetAllowedNextStatuses(ApplicationStatus current, decimal totalCompletedPaid)
    {
        var allowed = AllowedTransitions[current].AsEnumerable();

        if (totalCompletedPaid > 0)
        {
            allowed = allowed.Except(BlockedOncePaid);
        }

        return allowed.ToList();
    }

    /// <summary>
    /// Recomputes <see cref="Status"/> from the sum of currently-completed payments. Unlike
    /// <see cref="TransitionTo"/>, this bypasses <see cref="AllowedTransitions"/> deliberately: it
    /// is a system-computed correction (called after a payment is created or voided), not a
    /// reviewer-driven decision, so it must be able to move an application back down from Paid to
    /// PartiallyPaid (or to Approved) when a payment is voided. Only applies once the application
    /// has entered the payment lifecycle (Approved/PartiallyPaid/Paid) — an OnHold application
    /// stays OnHold even if a related payment is voided, since OnHold was a deliberate manual call.
    /// Returns the previous <see cref="Status"/> if this call actually changed it (so the caller can
    /// journal the transition to <c>application_status_history</c>), or <c>null</c> if it was a no-op.
    /// </summary>
    public ApplicationStatus? RecomputeStatusFromPayments(decimal totalCompletedPaid)
    {
        if (Status is not (ApplicationStatus.Approved or ApplicationStatus.PartiallyPaid or ApplicationStatus.Paid))
        {
            return null;
        }

        var previousStatus = Status;

        Status = ApprovedAmount is not null && totalCompletedPaid >= ApprovedAmount.Value
            ? ApplicationStatus.Paid
            : totalCompletedPaid > 0
                ? ApplicationStatus.PartiallyPaid
                : ApplicationStatus.Approved;

        return Status == previousStatus ? null : previousStatus;
    }
}
