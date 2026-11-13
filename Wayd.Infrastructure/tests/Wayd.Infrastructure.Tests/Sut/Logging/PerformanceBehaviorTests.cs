using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Wayd.Common.Application.Behaviors;
using Wayd.Common.Application.Interfaces;
using Wolverine;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

/// <summary>
/// Runs <see cref="PerformanceBehavior"/> through a real Serilog pipeline with the log context enricher,
/// so a payload pushed onto the context would show up in the event as it would in production. Here rather
/// than in the Common.Application tests because this project already carries Serilog.
/// </summary>
public sealed class PerformanceBehaviorTests
{
    private const string Secret = "pat_s3cr3t-value";

    [Fact]
    public void Finally_WhenSlow_LogsTypeAndElapsedWithoutThePayload()
    {
        // Arrange
        var (logger, sink) = CreateLogger();
        var envelope = new Envelope(new SecretBearingCommand("Connection", Secret));

        // Act
        PerformanceBehavior.Finally(TwoSecondsAgo(), logger, envelope);

        // Assert
        var logEvent = sink.Events.Should().ContainSingle().Subject;
        logEvent.Level.Should().Be(LogEventLevel.Warning);
        ((double)((ScalarValue)logEvent.Properties["ApplicationElapsed"]).Value!).Should().BeGreaterThanOrEqualTo(2000);
        logEvent.Properties.Keys.Should().BeEquivalentTo("AppRequestName", "ApplicationElapsed", "SourceContext");
        logEvent.Properties["AppRequestName"].ToString().Should().Be("\"SecretBearingCommand\"");
        logEvent.Properties.Values.Should().NotContain(v => v.ToString().Contains(Secret));
    }

    [Fact]
    public void Finally_WhenFast_LogsNothing()
    {
        // Arrange
        var (logger, sink) = CreateLogger();
        var envelope = new Envelope(new SecretBearingCommand("Connection", Secret));

        // Act
        PerformanceBehavior.Finally(Stopwatch.GetTimestamp(), logger, envelope);

        // Assert
        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public void Finally_WhenSlowButLongRunning_LogsNothing()
    {
        // Arrange
        var (logger, sink) = CreateLogger();
        var envelope = new Envelope(new LongRunningCommand());

        // Act
        PerformanceBehavior.Finally(TwoSecondsAgo(), logger, envelope);

        // Assert
        sink.Events.Should().BeEmpty();
    }

    private static long TwoSecondsAgo() => Stopwatch.GetTimestamp() - (2 * Stopwatch.Frequency);

    private static (ILogger<PerformanceBehaviorLog> Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var serilog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var factory = new SerilogLoggerFactory(serilog, dispose: true);

        return (factory.CreateLogger<PerformanceBehaviorLog>(), sink);
    }

    private sealed record SecretBearingCommand(string Name, string AccessToken);

    private sealed record LongRunningCommand : ILongRunningRequest;
}
