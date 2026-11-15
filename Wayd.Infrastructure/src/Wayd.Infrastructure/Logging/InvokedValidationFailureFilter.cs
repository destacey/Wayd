using Serilog.Core;
using Serilog.Events;

namespace Wayd.Infrastructure.Logging;

/// <summary>
/// Drops Wolverine's Error event for a command or query that an HTTP request invoked and that failed
/// validation. The request answers 422 and <c>ExceptionMiddleware</c> logs it, with the failures, at
/// Information: it is the caller's mistake, not a fault, and must not reach error alerting.
/// </summary>
/// <remarks>
/// Wolverine logs every failed <c>InvokeAsync</c> as <c>Invocation of {Message} failed!</c> at one level for
/// every exception (<c>MessageFailureLogLevel</c> is process-wide), so the level cannot be set per exception
/// there. Only events inside an HTTP request (with a <c>RequestId</c>) are dropped: a message handled off a
/// queue has no other record of its failure.
/// </remarks>
internal sealed class InvokedValidationFailureFilter : ILogEventFilter
{
    private const string InvocationFailedTemplate = "Invocation of {Message} failed!";

    /// <inheritdoc />
    public bool IsEnabled(LogEvent logEvent) =>
        !(logEvent.Exception is ValidationException
            && logEvent.MessageTemplate.Text == InvocationFailedTemplate
            && logEvent.Properties.ContainsKey("RequestId"));
}
