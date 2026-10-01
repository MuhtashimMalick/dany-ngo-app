namespace NgoFund.Contracts.Intake;

/// <summary>One answered question on the Google Form, exactly as Apps Script read it off the
/// <c>FormResponse</c>: <see cref="Title"/> is the question's visible title (possibly noisy —
/// dash variants, double spaces), <see cref="HelpText"/> disambiguates same-titled questions (the
/// three Education "File Upload" questions), and <see cref="Values"/> holds every selected value
/// for a checkbox question or the single value for every other question type.</summary>
public record GoogleFormAnswer(string Title, string? HelpText, IReadOnlyList<string> Values);
