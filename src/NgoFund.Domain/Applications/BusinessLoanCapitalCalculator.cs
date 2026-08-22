namespace NgoFund.Domain.Applications;

/// <summary>
/// The one place the Business Loan form's "does the applicant's own arithmetic add up" check
/// lives. Deliberately advisory, not a validation rule: staff must be able to re-key a form
/// faithfully even when <c>capital_required - capital_already_available</c> doesn't match
/// <c>applications.requested_amount</c> (the applicant's own math may simply be wrong), so this
/// returns a warning string for the response DTO rather than throwing.
/// </summary>
public static class BusinessLoanCapitalCalculator
{
    // ponytail: flat 5%-of-requested (floor 1.00) tolerance for "roughly match" — no product
    // definition of the acceptable gap exists yet. Replace with a client-specified threshold if
    // this ever needs to be exact.
    private const decimal ToleranceFraction = 0.05m;
    private const decimal MinimumTolerance = 1.00m;

    public static string? ComputeAmountMismatchWarning(decimal requestedAmount, decimal? capitalRequired, decimal? capitalAlreadyAvailable)
    {
        if (capitalRequired is null || capitalAlreadyAvailable is null)
        {
            return null;
        }

        var impliedGap = capitalRequired.Value - capitalAlreadyAvailable.Value;
        var difference = Math.Abs(impliedGap - requestedAmount);
        var tolerance = Math.Max(requestedAmount * ToleranceFraction, MinimumTolerance);

        if (difference <= tolerance)
        {
            return null;
        }

        return $"Capital required ({capitalRequired:N2}) minus capital already available ({capitalAlreadyAvailable:N2}) = {impliedGap:N2}, " +
               $"which does not roughly match the requested loan amount ({requestedAmount:N2}).";
    }
}
