using Serilog.Core;
using Serilog.Events;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

/// <summary>A Serilog sink that keeps every event it receives, for asserting on what a log event carries.</summary>
internal sealed class CollectingSink : ILogEventSink
{
    /// <summary>The events emitted so far, in order.</summary>
    public List<LogEvent> Events { get; } = [];

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
