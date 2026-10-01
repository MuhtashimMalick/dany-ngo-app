using NgoFund.Contracts.Intake;

namespace NgoFund.Application.Intake;

/// <summary>Indexes a submission's answers by normalized title for the per-category mapping
/// methods in <see cref="GoogleFormSubmissionMapper"/>, and tracks which ones were actually read
/// so <see cref="GetUnconsumed"/> can report the rest as unmapped — the source of the "every
/// unmapped answer becomes a note" rule (C5) without hand-maintaining a separate "known titles"
/// list per category.</summary>
internal sealed class GoogleFormAnswerLookup
{
    private readonly Dictionary<string, GoogleFormAnswer> _byTitle;
    private readonly HashSet<string> _consumed = new(StringComparer.OrdinalIgnoreCase);

    public GoogleFormAnswerLookup(IReadOnlyList<GoogleFormAnswer> answers)
    {
        _byTitle = new Dictionary<string, GoogleFormAnswer>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in answers)
        {
            _byTitle[FormValueParser.NormalizeTitle(answer.Title)] = answer;
        }
    }

    /// <summary>The single RAW value of a free-text/date/number question — never parenthetical-stripped.
    /// A free-text answer (an address, a health description) can legitimately contain " (" and must
    /// not be cut there — see <see cref="FormValueParser.StripOptionParenthetical"/>'s own doc comment.
    /// Use <see cref="GetOption"/> for a radio/dropdown answer instead.</summary>
    public string? GetString(string title)
    {
        var values = GetValues(title);
        return values.Count > 0 && !string.IsNullOrWhiteSpace(values[0]) ? values[0] : null;
    }

    /// <summary>The single value of a radio/dropdown question with its trailing " (...)"
    /// Urdu/explanatory parenthetical stripped — e.g. "Katchi (temporary structure)" -&gt; "Katchi".
    /// Never use this for free text.</summary>
    public string? GetOption(string title)
    {
        var raw = GetString(title);
        return raw is null ? null : FormValueParser.StripOptionParenthetical(raw);
    }

    /// <summary>Every RAW selected value of a checkbox question (or the single value of any other
    /// question type) — unstripped.</summary>
    public IReadOnlyList<string> GetValues(string title)
    {
        var key = FormValueParser.NormalizeTitle(title);
        if (!_byTitle.TryGetValue(key, out var answer))
        {
            return [];
        }

        _consumed.Add(key);
        return answer.Values.Select(v => v.Trim()).ToList();
    }

    /// <summary>Whether a checkbox question has the given option (compared after parenthetical
    /// stripping, since checkbox option text carries the Urdu translation) selected.</summary>
    public bool Contains(string title, string optionValue) =>
        GetValues(title).Any(v => string.Equals(FormValueParser.StripOptionParenthetical(v), optionValue, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every answer never read via <see cref="GetString"/>/<see cref="GetOption"/>/
    /// <see cref="GetValues"/> — becomes a line in the intake-notes remark.</summary>
    public IReadOnlyList<GoogleFormAnswer> GetUnconsumed() =>
        _byTitle.Where(kv => !_consumed.Contains(kv.Key)).Select(kv => kv.Value).ToList();
}
