using NgoFund.Contracts.Reports;

namespace NgoFund.Application.Reports;

/// <summary>
/// Pure calculation of application decision turnaround time — no EF/DB dependency so it is
/// unit-testable in isolation. The caller is responsible for fetching the raw
/// (application, application date, decision timestamp) rows within whatever window applies.
/// </summary>
public static class ProcessingTimeCalculator
{
    public static ProcessingTimeDto Calculate(
        IReadOnlyList<(Guid ApplicationId, DateOnly ApplicationDate, DateTimeOffset DecidedAt)> decisions,
        int windowDays)
    {
        // An application can be approved, put OnHold, then approved again — only the earliest
        // decision counts as "time to first decision".
        var earliestPerApplication = decisions
            .GroupBy(d => d.ApplicationId)
            .Select(g => g.OrderBy(d => d.DecidedAt).First());

        // Same clamp as Dashboard.razor's DaysWaiting: a decision timestamp can't precede the
        // application date in a healthy dataset, but clamp defensively rather than surface a
        // negative "days to decide".
        var days = earliestPerApplication
            .Select(d => Math.Max(0, DateOnly.FromDateTime(d.DecidedAt.Date).DayNumber - d.ApplicationDate.DayNumber))
            .OrderBy(d => d)
            .ToList();

        if (days.Count == 0)
        {
            return new ProcessingTimeDto(null, null, 0, windowDays);
        }

        var average = days.Average();
        var median = days.Count % 2 == 1
            ? days[days.Count / 2]
            : (days[days.Count / 2 - 1] + days[days.Count / 2]) / 2.0;

        return new ProcessingTimeDto(average, median, days.Count, windowDays);
    }
}
