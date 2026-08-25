// HARD CONSTRAINT: Narrate must be pure. It may read only what's already in the change tracker
// (entry.Property(...).OriginalValue/CurrentValue, entry.Reference(...).TargetEntry — only when
// already loaded/tracked, never triggering a lazy load). It must NEVER query the database or read
// AppSettings. It runs inside SavingChanges, inside a transaction that in PaymentService is
// holding a SELECT ... FOR UPDATE row lock on the application — a lazy load here is a latent
// deadlock. Corollary: no money amounts or currency symbols in sentences (would need an
// app_settings lookup) — none of the sentences below need one.

using Microsoft.EntityFrameworkCore.ChangeTracking;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Infrastructure.Identity;

namespace NgoFund.Infrastructure.Persistence.Auditing;

internal sealed record ActivityNarration(string EntityLabel, string? EntityNumber, ActivityVerb Verb, string Summary);

/// <summary>
/// Turns one changed entity into the client-facing activity-feed sentence, or null when nothing
/// in the table below matches. See the hard constraint above before touching this file.
/// </summary>
internal static class ActivityNarrator
{
    internal static ActivityNarration? Narrate(EntityEntry entry, string efAction) => entry.Entity switch
    {
        Donor donor => NarrateDonor(donor, efAction),
        Donation donation => NarrateDonation(entry, donation, efAction),
        Applicant applicant => NarrateApplicant(entry, applicant, efAction),
        FundApplication application => NarrateFundApplication(entry, application, efAction),
        Payment payment => NarratePayment(entry, payment, efAction),
        LoanAgreement loan => NarrateLoanAgreement(entry, loan, efAction),
        LoanRepayment repayment => NarrateLoanRepayment(entry, repayment, efAction),
        ApplicationUser user => NarrateApplicationUser(entry, user, efAction),
        FundCategory fund => NarrateFundCategory(fund, efAction),
        ApplicationCategory category => NarrateApplicationCategory(category, efAction),
        AppSetting setting => NarrateAppSetting(setting, efAction),
        RolePermission rolePermission => NarrateRolePermission(entry, rolePermission, efAction),
        _ => null,
    };

    private static ActivityNarration? NarrateDonor(Donor donor, string efAction) => efAction switch
    {
        "Create" => new("Donor", donor.DonorCode, ActivityVerb.Created, $"Donor \"{donor.DonorCode}\" ({donor.FullName}) was added"),
        "Update" => new("Donor", donor.DonorCode, ActivityVerb.Updated, $"Donor \"{donor.DonorCode}\" was updated"),
        "SoftDelete" => new("Donor", donor.DonorCode, ActivityVerb.Deleted, $"Donor \"{donor.DonorCode}\" was deleted"),
        _ => null,
    };

    private static ActivityNarration? NarrateDonation(EntityEntry entry, Donation donation, string efAction)
    {
        if (efAction == "Create")
        {
            return new("Donation", donation.DonationNumber, ActivityVerb.Recorded, $"Donation \"{donation.DonationNumber}\" was recorded");
        }

        if (efAction == "Update")
        {
            if (IsModifiedTo(entry, nameof(Donation.Status), DonationStatus.Voided))
            {
                return new("Donation", donation.DonationNumber, ActivityVerb.Voided, $"Donation \"{donation.DonationNumber}\" was voided");
            }

            return new("Donation", donation.DonationNumber, ActivityVerb.Updated, $"Donation \"{donation.DonationNumber}\" was updated");
        }

        return null;
    }

    private static ActivityNarration? NarrateApplicant(EntityEntry entry, Applicant applicant, string efAction)
    {
        if (efAction == "Create")
        {
            return new("Applicant", applicant.MembershipNumber, ActivityVerb.Created, $"Applicant \"{applicant.FullName}\" was added");
        }

        if (efAction == "Update")
        {
            var blacklistTransition = GetBoolTransition(entry, nameof(Applicant.IsBlacklisted));
            if (blacklistTransition == (false, true))
            {
                return new("Applicant", applicant.MembershipNumber, ActivityVerb.Blacklisted, $"Applicant \"{applicant.FullName}\" was blacklisted");
            }

            if (blacklistTransition == (true, false))
            {
                return new("Applicant", applicant.MembershipNumber, ActivityVerb.Reactivated, $"Applicant \"{applicant.FullName}\" was removed from the blacklist");
            }

            return new("Applicant", applicant.MembershipNumber, ActivityVerb.Updated, $"Applicant \"{applicant.FullName}\" was updated");
        }

        if (efAction == "SoftDelete")
        {
            return new("Applicant", applicant.MembershipNumber, ActivityVerb.Deleted, $"Applicant \"{applicant.FullName}\" was deleted");
        }

        return null;
    }

    private static ActivityNarration? NarrateFundApplication(EntityEntry entry, FundApplication application, string efAction)
    {
        if (efAction == "Create")
        {
            return new("Application", application.ApplicationNumber, ActivityVerb.Created, $"Application \"{application.ApplicationNumber}\" was created");
        }

        if (efAction == "Update")
        {
            var statusProperty = entry.Property(nameof(FundApplication.Status));
            if (statusProperty.ActuallyChanged())
            {
                var from = (ApplicationStatus)statusProperty.OriginalValue!;
                var to = (ApplicationStatus)statusProperty.CurrentValue!;
                return new("Application", application.ApplicationNumber, ActivityVerb.StatusChanged,
                    $"Application \"{application.ApplicationNumber}\" status changed from {from} to {to}");
            }

            return new("Application", application.ApplicationNumber, ActivityVerb.Updated, $"Application \"{application.ApplicationNumber}\" was updated");
        }

        return null;
    }

    private static ActivityNarration? NarratePayment(EntityEntry entry, Payment payment, string efAction)
    {
        if (efAction == "Create")
        {
            return new("Payment", payment.PaymentNumber, ActivityVerb.Recorded, $"Payment \"{payment.PaymentNumber}\" was recorded");
        }

        if (efAction == "Update" && IsModifiedTo(entry, nameof(Payment.Status), PaymentStatus.Voided))
        {
            return new("Payment", payment.PaymentNumber, ActivityVerb.Voided, $"Payment \"{payment.PaymentNumber}\" was voided");
        }

        return null;
    }

    private static ActivityNarration? NarrateLoanAgreement(EntityEntry entry, LoanAgreement loan, string efAction)
    {
        if (efAction == "Create")
        {
            var applicationEntry = entry.Reference(nameof(LoanAgreement.Application)).TargetEntry;
            var suffix = applicationEntry?.Entity is FundApplication application
                ? $" for application \"{application.ApplicationNumber}\""
                : string.Empty;
            return new("Loan agreement", loan.LoanNumber, ActivityVerb.Created, $"Loan agreement \"{loan.LoanNumber}\" was created{suffix}");
        }

        if (efAction == "Update")
        {
            if (IsModifiedTo(entry, nameof(LoanAgreement.Status), LoanAgreementStatus.Cancelled))
            {
                return new("Loan agreement", loan.LoanNumber, ActivityVerb.Cancelled, $"Loan agreement \"{loan.LoanNumber}\" was cancelled");
            }

            if (IsModifiedTo(entry, nameof(LoanAgreement.Status), LoanAgreementStatus.WrittenOff))
            {
                return new("Loan agreement", loan.LoanNumber, ActivityVerb.WrittenOff, $"Loan agreement \"{loan.LoanNumber}\" was written off");
            }
        }

        return null;
    }

    private static ActivityNarration? NarrateLoanRepayment(EntityEntry entry, LoanRepayment repayment, string efAction)
    {
        if (efAction == "Create")
        {
            return new("Loan repayment", repayment.RepaymentNumber, ActivityVerb.Recorded, $"Loan repayment \"{repayment.RepaymentNumber}\" was recorded");
        }

        if (efAction == "Update" && IsModifiedTo(entry, nameof(LoanRepayment.Status), LoanRepaymentStatus.Voided))
        {
            return new("Loan repayment", repayment.RepaymentNumber, ActivityVerb.Voided, $"Loan repayment \"{repayment.RepaymentNumber}\" was voided");
        }

        return null;
    }

    private static ActivityNarration? NarrateApplicationUser(EntityEntry entry, ApplicationUser user, string efAction)
    {
        if (efAction == "Create")
        {
            return new("User", null, ActivityVerb.Created, $"User \"{user.FullName}\" was created");
        }

        if (efAction == "Update")
        {
            var activeTransition = GetBoolTransition(entry, nameof(ApplicationUser.IsActive));
            if (activeTransition == (true, false))
            {
                return new("User", null, ActivityVerb.Deactivated, $"User \"{user.FullName}\" was deactivated");
            }

            if (activeTransition == (false, true))
            {
                return new("User", null, ActivityVerb.Reactivated, $"User \"{user.FullName}\" was reactivated");
            }

            if (entry.Property(nameof(ApplicationUser.PasswordHash)).ActuallyChanged())
            {
                return new("User", null, ActivityVerb.Updated, $"Password for user \"{user.FullName}\" was changed");
            }

            return new("User", null, ActivityVerb.Updated, $"User \"{user.FullName}\" was updated");
        }

        return null;
    }

    private static ActivityNarration? NarrateFundCategory(FundCategory fund, string efAction) => efAction switch
    {
        "Create" => new("Fund", fund.Code, ActivityVerb.Created, $"Fund \"{fund.Name}\" was added"),
        "Update" => new("Fund", fund.Code, ActivityVerb.Updated, $"Fund \"{fund.Name}\" was updated"),
        "SoftDelete" => new("Fund", fund.Code, ActivityVerb.Deleted, $"Fund \"{fund.Name}\" was deleted"),
        _ => null,
    };

    private static ActivityNarration? NarrateApplicationCategory(ApplicationCategory category, string efAction) => efAction switch
    {
        "Create" => new("Application category", category.Code, ActivityVerb.Created, $"Application category \"{category.Name}\" was added"),
        "Update" => new("Application category", category.Code, ActivityVerb.Updated, $"Application category \"{category.Name}\" was updated"),
        "SoftDelete" => new("Application category", category.Code, ActivityVerb.Deleted, $"Application category \"{category.Name}\" was deleted"),
        _ => null,
    };

    private static ActivityNarration? NarrateAppSetting(AppSetting setting, string efAction) => efAction switch
    {
        "Update" => new("Setting", setting.Key, ActivityVerb.Updated, $"Setting \"{setting.Key}\" was updated"),
        _ => null,
    };

    private static ActivityNarration? NarrateRolePermission(EntityEntry entry, RolePermission rolePermission, string efAction)
    {
        if (efAction is not ("Create" or "Delete"))
        {
            return null;
        }

        // Never query: only use the Role/Permission navigations if they're already tracked. There
        // is no raw name/code duplicated on the join row itself to fall back to, so an untracked
        // reference means this change simply isn't narrated.
        var roleEntry = entry.Reference(nameof(RolePermission.Role)).TargetEntry;
        var permissionEntry = entry.Reference(nameof(RolePermission.Permission)).TargetEntry;

        if (roleEntry?.Entity is not ApplicationRole role || permissionEntry?.Entity is not Permission permission)
        {
            return null;
        }

        return efAction == "Create"
            ? new("Role permission", null, ActivityVerb.PermissionGranted, $"Role \"{role.Name}\" was granted permission \"{permission.DisplayName}\"")
            : new("Role permission", null, ActivityVerb.PermissionRevoked, $"Role \"{role.Name}\" had permission \"{permission.DisplayName}\" revoked");
    }

    private static bool IsModifiedTo<TEnum>(EntityEntry entry, string propertyName, TEnum expectedValue)
        where TEnum : struct, Enum
    {
        var property = entry.Property(propertyName);
        return property.ActuallyChanged() && expectedValue.Equals((TEnum)property.CurrentValue!);
    }

    /// <summary>Returns (Original, Current) for an actually-changed bool property, or null when it didn't really change.</summary>
    private static (bool Original, bool Current)? GetBoolTransition(EntityEntry entry, string propertyName)
    {
        var property = entry.Property(propertyName);
        if (!property.ActuallyChanged())
        {
            return null;
        }

        return ((bool)property.OriginalValue!, (bool)property.CurrentValue!);
    }
}
