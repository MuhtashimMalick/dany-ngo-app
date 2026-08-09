using System.IO;
using Microsoft.Extensions.Logging;

namespace NgoFund.Desktop.Logging;

/// <summary>
/// Minimal file logger for this desktop process — there is no console window to write to, and
/// exceptions during Blazor component rendering only surface through <see cref="ILogger"/>, not
/// as WPF-level exceptions. Logs to <c>%LocalAppData%\NgoFund\desktop.log</c> so a user can send
/// it in when something goes wrong.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NgoFund", "desktop.log");

    private readonly object _lock = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, Write);

    public void Dispose() { }

    private void Write(string message)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, message + Environment.NewLine);
        }
    }

    private class FileLogger(string categoryName, Action<string> write) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            write($"{DateTimeOffset.Now:O} [{logLevel}] {categoryName}: {formatter(state, exception)}{(exception is null ? "" : Environment.NewLine + exception)}");
        }
    }
}
