using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wayd.Common.Application.Behaviors;

/// <summary>
/// Wolverine middleware that warns when a message takes longer than the threshold to handle, unless
/// the message opts out via <see cref="ILongRunningRequest"/>.
/// </summary>
/// <remarks>
/// The warning carries the message's type name and elapsed time, never the message itself: commands carry
/// credentials, personal data and free text, and this path runs for every message type regardless of what
/// its handler chooses to redact.
/// </remarks>
public static class PerformanceBehavior
{
    private const long ThresholdMilliseconds = 700;

    /// <summary>Captures the start timestamp before the handler runs.</summary>
    public static long Before() => Stopwatch.GetTimestamp();

    /// <summary>Logs a warning when the handler ran past the threshold.</summary>
    public static void Finally(
        long startTimestamp,
        ILogger<PerformanceBehaviorLog> logger,
        Envelope envelope)
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        // Read the message off the envelope rather than taking an `object message` middleware parameter:
        // Wolverine cannot bind a raw `object` parameter to the message and would try to service-locate
        // System.Object instead, which fails at dispatch.
        var message = envelope.Message;

        if (elapsedMilliseconds > ThresholdMilliseconds && message is not null && message is not ILongRunningRequest)
        {
            logger.LogWarning("Long running request: {AppRequestName} completed in {ApplicationElapsed} ms", message.GetType().Name, elapsedMilliseconds);
        }
    }
}

/// <summary>
/// Log category for <see cref="PerformanceBehavior"/>, giving its warnings one stable source context;
/// Wolverine would otherwise inject an <c>ILogger&lt;T&gt;</c> typed to each message.
/// </summary>
public sealed class PerformanceBehaviorLog
{
}
