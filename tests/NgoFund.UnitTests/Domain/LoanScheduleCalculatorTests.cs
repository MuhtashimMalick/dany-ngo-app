using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Domain.Loans;
using static NgoFund.Domain.Loans.LoanScheduleCalculator;

namespace NgoFund.UnitTests.Domain;

public class LoanScheduleCalculatorTests
{
    private static readonly DateOnly FirstDueDate = new(2026, 1, 1);

    [Fact]
    public void GenerateInstallments_EvenDivision_AllInstallmentsEqual()
    {
        var lines = GenerateInstallments(1000m, 4, FirstDueDate, LoanInstallmentFrequency.Monthly);

        Assert.All(lines, l => Assert.Equal(250m, l.AmountDue));
        Assert.Equal(1000m, lines.Sum(l => l.AmountDue));
    }

    [Fact]
    public void GenerateInstallments_RemainderGoesToLastInstallment()
    {
        var lines = GenerateInstallments(1000m, 3, FirstDueDate, LoanInstallmentFrequency.Monthly);

        Assert.Equal(333.33m, lines[0].AmountDue);
        Assert.Equal(333.33m, lines[1].AmountDue);
        Assert.Equal(333.34m, lines[2].AmountDue);
        Assert.Equal(1000m, lines.Sum(l => l.AmountDue));
    }

    [Fact]
    public void GenerateInstallments_CountOfOne_SingleInstallmentEqualsPrincipal()
    {
        var lines = GenerateInstallments(5000m, 1, FirstDueDate, LoanInstallmentFrequency.Monthly);

        Assert.Single(lines);
        Assert.Equal(5000m, lines[0].AmountDue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateInstallments_CountBelowOne_Throws(int count)
    {
        Assert.Throws<InvalidInstallmentPlanException>(() =>
            GenerateInstallments(1000m, count, FirstDueDate, LoanInstallmentFrequency.Monthly));
    }

    [Fact]
    public void GenerateInstallments_BaseAmountBelowMinimum_Throws()
    {
        // 1 rupee split across 200 installments -> base = trunc(0.005, 2) = 0.00, below the 0.01 floor.
        Assert.Throws<InvalidInstallmentPlanException>(() =>
            GenerateInstallments(1m, 200, FirstDueDate, LoanInstallmentFrequency.Monthly));
    }

    [Fact]
    public void GenerateInstallments_MonthEndFirstDueDate_RollsOverCorrectly()
    {
        // Jan 31 + 1 month must land on a valid February date (DateOnly.AddMonths clamps, doesn't throw).
        var lines = GenerateInstallments(300m, 3, new DateOnly(2026, 1, 31), LoanInstallmentFrequency.Monthly);

        Assert.Equal(new DateOnly(2026, 1, 31), lines[0].DueDate);
        Assert.Equal(new DateOnly(2026, 2, 28), lines[1].DueDate); // clamped: 2026 is not a leap year
        // DateOnly.AddMonths operates on the actual (already-clamped) Feb 28 date, not the
        // original day-of-month 31 — Feb 28 + 1 month is a valid Mar 28, no further clamping.
        Assert.Equal(new DateOnly(2026, 3, 28), lines[2].DueDate);
    }

    // --- The "no interest" guarantee: SUM(AmountDue) == principal, exactly, across a wide sweep. ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1000, 3)]
    [InlineData(1000, 7)]
    [InlineData(999.99, 11)]
    [InlineData(50000, 12)]
    [InlineData(123456.78, 24)]
    [InlineData(10, 5)]
    [InlineData(0.03, 3)]
    [InlineData(7654321.99, 60)]
    public void GenerateInstallments_SumAlwaysEqualsPrincipal(decimal principal, int count)
    {
        var lines = GenerateInstallments(principal, count, FirstDueDate, LoanInstallmentFrequency.Monthly);

        Assert.Equal(principal, lines.Sum(l => l.AmountDue));
        Assert.Equal(count, lines.Count);
    }

    [Fact]
    public void Allocate_PartialRepayment_SpansTwoInstallments()
    {
        var lines = GenerateInstallments(900m, 3, FirstDueDate, LoanInstallmentFrequency.Monthly); // 300 each

        var allocated = Allocate(lines, totalRepaid: 450m, principalDisbursed: 900m, asOfDate: FirstDueDate);

        Assert.Equal(300m, allocated[0].AmountAllocated);
        Assert.Equal(0m, allocated[0].AmountRemaining);
        Assert.Equal(InstallmentAllocationStatus.Paid, allocated[0].Status);

        Assert.Equal(150m, allocated[1].AmountAllocated);
        Assert.Equal(150m, allocated[1].AmountRemaining);
        Assert.Equal(InstallmentAllocationStatus.PartiallyPaid, allocated[1].Status);

        Assert.Equal(0m, allocated[2].AmountAllocated);
        Assert.Equal(300m, allocated[2].AmountRemaining);
    }

    [Fact]
    public void Allocate_BeyondDisbursedPrincipal_IsUndisbursedNotOverdue()
    {
        var lines = GenerateInstallments(900m, 3, new DateOnly(2020, 1, 1), LoanInstallmentFrequency.Monthly); // long overdue by date

        // Only the first installment's worth of principal has actually been disbursed.
        var allocated = Allocate(lines, totalRepaid: 0m, principalDisbursed: 300m, asOfDate: DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(InstallmentAllocationStatus.Overdue, allocated[0].Status);
        Assert.Equal(InstallmentAllocationStatus.Undisbursed, allocated[1].Status);
        Assert.Equal(InstallmentAllocationStatus.Undisbursed, allocated[2].Status);
    }

    [Fact]
    public void Allocate_NotYetDue_FullyDisbursed_IsPending()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var lines = GenerateInstallments(300m, 1, future, LoanInstallmentFrequency.Monthly);

        var allocated = Allocate(lines, totalRepaid: 0m, principalDisbursed: 300m, asOfDate: DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(InstallmentAllocationStatus.Pending, allocated[0].Status);
    }
}
