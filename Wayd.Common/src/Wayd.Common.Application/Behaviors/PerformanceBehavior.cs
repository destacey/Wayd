using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Wolverine;

namespace Wayd.Common.Application.Behaviors;

/// <summary>
/// Wolverine middleware that warns when a message takes longer than the threshold to handle, unless
/// the message opts out via <see cref="ILongRunningRequest"/>. Ported from the MediatR
/// <c>PerformanceBehavior</c> pipeline behavior with identical thresholds and logging.
/// </summary>
public static class PerformanceBehavior
{
    private const long ThresholdMilliseconds = 700;

    public static long Before() => Stopwatch.GetTimestamp();

    public static void Finally(
        long startTimestamp,
        ILogger<PerformanceBehaviorLog> logger,
        ISerializerService jsonSerializer,
        Envelope envelope)
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        // Read the message off the envelope rather than taking an `object message` middleware parameter:
        // Wolverine cannot bind a raw `object` parameter to the message and would try to service-locate
        // System.Object instead, which fails at dispatch.
        var message = envelope.Message;

        if (elapsedMilliseconds > ThresholdMilliseconds && message is not null && message is not ILongRunningRequest)
        {
            var requestName = message.GetType().Name;

            using (LogContext.PushProperty("ApplicationRequestModel", jsonSerializer.Serialize(message)))
            {
                logger.LogWarning("Long running request: {AppRequestName} completed in {ApplicationElapsed} ms", requestName, elapsedMilliseconds);
            }
        }
    }
}

/// <summary>
/// Log category marker for <see cref="PerformanceBehavior"/> so the emitted warnings keep a stable,
/// recognisable source context (Wolverine injects <c>ILogger&lt;T&gt;</c> typed to the message, so we
/// use this instead to preserve the previous category name).
/// </summary>
public sealed class PerformanceBehaviorLog
{
}
