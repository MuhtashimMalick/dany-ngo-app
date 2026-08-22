using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;

namespace NgoFund.Domain.Loans;

/// <summary>
/// The ONE place installment rounding and repayment allocation happens (D3/D5) — never duplicated
/// in a service or in SQL. Pure/static: no DB access, no clock reads except the explicit
/// <c>asOfDate</c> parameter <see cref="Allocate"/> takes for "is this line overdue", which keeps
/// every method here a deterministic function of its inputs and testable without mocking time.
/// </summary>
public static class LoanScheduleCalculator
{
    /// <summary>Below this, an installment amount is not a meaningful currency value.</summary>
    private const decimal MinInstallmentAmount = 0.01m;

    /// <summary>One line of a freshly generated (not yet persisted) installment schedule.</summary>
    public sealed record InstallmentPlanLine(int SequenceNumber, decimal AmountDue, DateOnly DueDate);

    /// <summary>One installment with its repayment allocation and derived status, computed at read time.</summary>
    public sealed record AllocatedInstallment(
        int SequenceNumber,
        decimal AmountDue,
        DateOnly DueDate,
        decimal AmountAllocated,
        decimal AmountRemaining,
        InstallmentAllocationStatus Status);

    public enum InstallmentAllocationStatus
    {
        /// <summary>Fully repaid.</summary>
        Paid,

        /// <summary>Partially repaid.</summary>
        PartiallyPaid,

        /// <summary>Not yet due, unpaid, and fully covered by disbursed principal.</summary>
        Pending,

        /// <summary>Past its due date, unpaid, and covered by disbursed principal.</summary>
        Overdue,

        /// <summary>
        /// Not (yet) covered by disbursed principal at all — per D3, this is reported instead of
        /// "Overdue" even if the due date has passed, since no money against this line has
        /// actually gone out yet.
        /// </summary>
        Undisbursed,
    }

    /// <summary>
    /// Splits <paramref name="principal"/> into <paramref name="count"/> installments per D5's
    /// zero-interest rounding rule: <c>base = trunc(principal / count, 2)</c>, every installment
    /// equals <c>base</c> except the last, which absorbs the rounding remainder — so
    /// <c>SUM(AmountDue) == principal</c> exactly, always.
    /// </summary>
    public static IReadOnlyList<InstallmentPlanLine> GenerateInstallments(
        decimal principal, int count, DateOnly firstDueDate, LoanInstallmentFrequency frequency)
    {
        if (principal <= 0)
        {
            throw new InvalidInstallmentPlanException($"Principal must be positive; was {principal:N2}.");
        }

        if (count < 1)
        {
            throw new InvalidInstallmentPlanException($"Installment count must be at least 1; was {count}.");
        }

        var baseAmount = Math.Truncate(principal / count * 100m) / 100m;

        if (baseAmount < MinInstallmentAmount)
        {
            throw new InvalidInstallmentPlanException(
                $"Splitting a principal of {principal:N2} across {count} installments would produce an installment below {MinInstallmentAmount:N2}.");
        }

        var lines = new List<InstallmentPlanLine>(count);
        var dueDate = firstDueDate;
        var runningTotal = 0m;

        for (var sequence = 1; sequence <= count; sequence++)
        {
            // Last installment absorbs whatever the truncation above left on the table, so the
            // schedule always sums to exactly `principal` — this is the "no interest" guarantee.
            var amount = sequence < count ? baseAmount : principal - runningTotal;
            lines.Add(new InstallmentPlanLine(sequence, amount, dueDate));
            runningTotal += amount;
            dueDate = AddFrequency(dueDate, frequency);
        }

        return lines;
    }

    private static DateOnly AddFrequency(DateOnly date, LoanInstallmentFrequency frequency) => frequency switch
    {
        LoanInstallmentFrequency.Monthly => date.AddMonths(1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported installment frequency."),
    };

    /// <summary>
    /// Allocates <paramref name="totalRepaid"/> across <paramref name="installments"/> oldest-first
    /// (a waterfall — a partial repayment can span multiple lines) and derives each line's status
    /// from <paramref name="principalDisbursed"/> and <paramref name="asOfDate"/> (D3). Per D6,
    /// nothing about "who paid how much on which line" is ever stored — this recomputes it fresh
    /// from the ledger every time it's called.
    /// </summary>
    public static IReadOnlyList<AllocatedInstallment> Allocate(
        IReadOnlyList<InstallmentPlanLine> installments, decimal totalRepaid, decimal principalDisbursed, DateOnly asOfDate)
    {
        var ordered = installments.OrderBy(i => i.SequenceNumber).ToList();
        var remainingRepayment = Math.Max(totalRepaid, 0m);
        var cumulativeDueBefore = 0m;
        var result = new List<AllocatedInstallment>(ordered.Count);

        foreach (var line in ordered)
        {
            // How much of THIS line's due amount is actually unlocked by disbursed principal —
            // the windowed form of D3's `MIN(scheduled_due_to_date, principal_disbursed)`.
            var disbursedThroughEnd = Math.Clamp(principalDisbursed - cumulativeDueBefore, 0m, line.AmountDue);
            cumulativeDueBefore += line.AmountDue;

            var allocated = Math.Min(remainingRepayment, line.AmountDue);
            remainingRepayment -= allocated;
            var remaining = line.AmountDue - allocated;

            var status = remaining <= 0m
                ? InstallmentAllocationStatus.Paid
                : disbursedThroughEnd <= 0m
                    ? InstallmentAllocationStatus.Undisbursed
                    : allocated > 0m
                        ? InstallmentAllocationStatus.PartiallyPaid
                        : line.DueDate < asOfDate
                            ? InstallmentAllocationStatus.Overdue
                            : InstallmentAllocationStatus.Pending;

            result.Add(new AllocatedInstallment(line.SequenceNumber, line.AmountDue, line.DueDate, allocated, remaining, status));
        }

        return result;
    }
}
