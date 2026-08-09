namespace NgoFund.Desktop.Components.Shared;

/// <summary>
/// "3h ago" / "2d ago" style relative timestamps for the Applications manage modal's status
/// timeline and remarks list — replaces raw "yyyy-MM-dd HH:mm" stamps that made both lists read
/// as a database dump rather than an activity feed.
/// </summary>
public static class RelativeTimeFormat
{
    public static string From(DateTimeOffset when)
    {
        var span = DateTimeOffset.UtcNow - when.ToUniversalTime();
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        return span switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)span.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)span.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)span.TotalDays}d ago",
            { TotalDays: < 365 } => $"{(int)(span.TotalDays / 30)}mo ago",
            _ => $"{(int)(span.TotalDays / 365)}y ago",
        };
    }
}
