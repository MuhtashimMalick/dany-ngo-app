using NgoFund.Application.Reports;

namespace NgoFund.UnitTests.Reports;

public class ProcessingTimeCalculatorTests
{
    private static (Guid ApplicationId, DateOnly ApplicationDate, DateTimeOffset DecidedAt) Decision(
        DateOnly applicationDate, DateTimeOffset decidedAt, Guid? applicationId = null) =>
        (applicationId ?? Guid.NewGuid(), applicationDate, decidedAt);

    [Fact]
    public void Calculate_EmptyInput_ReturnsAllNullsWithZeroCount()
    {
        var result = ProcessingTimeCalculator.Calculate([], windowDays: 365);

        Assert.Null(result.AverageDays);
        Assert.Null(result.MedianDays);
        Assert.Equal(0, result.DecidedCount);
        Assert.Equal(365, result.WindowDays);
    }

    [Fact]
    public void Calculate_SingleDecision_ReturnsExactDayCount()
    {
        var applicationDate = new DateOnly(2026, 1, 1);
        var decidedAt = new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero);

        var result = ProcessingTimeCalculator.Calculate([Decision(applicationDate, decidedAt)], windowDays: 365);

        Assert.Equal(5, result.AverageDays);
        Assert.Equal(5, result.MedianDays);
        Assert.Equal(1, result.DecidedCount);
    }

    [Fact]
    public void Calculate_DecisionBeforeApplicationDate_ClampsToZeroDays()
    {
        var applicationDate = new DateOnly(2026, 1, 10);
        var decidedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = ProcessingTimeCalculator.Calculate([Decision(applicationDate, decidedAt)], windowDays: 365);

        Assert.Equal(0, result.AverageDays);
        Assert.Equal(0, result.MedianDays);
    }

    [Fact]
    public void Calculate_ApplicationDecidedTwice_UsesEarliestDecisionOnly()
    {
        var applicationId = Guid.NewGuid();
        var applicationDate = new DateOnly(2026, 1, 1);

        // Approved on day 20, put OnHold, then approved again on day 40 — only day 20 counts.
        var decisions = new[]
        {
            Decision(applicationDate, new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero), applicationId),
            Decision(applicationDate, new DateTimeOffset(2026, 1, 21, 0, 0, 0, TimeSpan.Zero), applicationId),
        };

        var result = ProcessingTimeCalculator.Calculate(decisions, windowDays: 365);

        Assert.Equal(1, result.DecidedCount);
        Assert.Equal(20, result.AverageDays);
    }

    [Fact]
    public void Calculate_OddCountOfDataPoints_MedianIsMiddleValue()
    {
        var applicationDate = new DateOnly(2026, 1, 1);
        var decisions = new[]
        {
            Decision(applicationDate, applicationDate.AddDays(2).ToDateTime(TimeOnly.MinValue)),
            Decision(applicationDate, applicationDate.AddDays(4).ToDateTime(TimeOnly.MinValue)),
            Decision(applicationDate, applicationDate.AddDays(10).ToDateTime(TimeOnly.MinValue)),
        };

        var result = ProcessingTimeCalculator.Calculate(decisions, windowDays: 365);

        Assert.Equal(4, result.MedianDays);
    }

    [Fact]
    public void Calculate_EvenCountOfDataPoints_MedianIsAverageOfMiddleTwo()
    {
        var applicationDate = new DateOnly(2026, 1, 1);
        var decisions = new[]
        {
            Decision(applicationDate, applicationDate.AddDays(2).ToDateTime(TimeOnly.MinValue)),
            Decision(applicationDate, applicationDate.AddDays(4).ToDateTime(TimeOnly.MinValue)),
            Decision(applicationDate, applicationDate.AddDays(6).ToDateTime(TimeOnly.MinValue)),
            Decision(applicationDate, applicationDate.AddDays(10).ToDateTime(TimeOnly.MinValue)),
        };

        var result = ProcessingTimeCalculator.Calculate(decisions, windowDays: 365);

        Assert.Equal(5, result.MedianDays);
    }
}
