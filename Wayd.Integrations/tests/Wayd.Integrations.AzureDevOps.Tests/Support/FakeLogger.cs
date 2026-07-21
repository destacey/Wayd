using Microsoft.Extensions.Logging;

namespace Wayd.Integrations.AzureDevOps.Tests.Support;

/// <summary>
/// Minimal <see cref="ILogger"/> double that records each logged entry's level and rendered
/// message, for tests that assert on whether (and at what level) something was logged.
/// </summary>
public sealed class FakeLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}
