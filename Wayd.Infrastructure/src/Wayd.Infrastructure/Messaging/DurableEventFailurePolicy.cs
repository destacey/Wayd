using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Failure policy for the durable event handler chains (see <see cref="DurableEventRoutes"/>): retry with
/// a bounded cooldown on any exception, then dead-letter on exhaustion.
/// </summary>
/// <remarks>
/// <para>
/// Scoped to the durable message types rather than registered globally. Wolverine applies retry/cooldown
/// rules to <c>InvokeAsync</c> too (it retries inline, then rethrows), so a global policy would also govern
/// the inline events — swallowing/retrying exceptions that are meant to bubble straight to
/// <c>ExceptionMiddleware</c>. The dead-letter continuation is only meaningful for the queued/background
/// durable path in any case.
/// </para>
/// <para>
/// The cooldown (1s, 5s, 15s) rides out the transient failures a background projection hits — a brief DB
/// blip, a deadlock, a connection reset — without hammering. On exhaustion the envelope moves to
/// <c>wolverine.dead_letters</c> for inspection/replay rather than being lost or retried forever. Durable
/// delivery is at-least-once, so durable handlers must be idempotent for a redelivery after a partial
/// failure to be safe.
/// </para>
/// </remarks>
public sealed class DurableEventFailurePolicy : IHandlerPolicy
{
    // Retry delays before dead-lettering. Spaced to ride out a transient DB/network blip, short enough that
    // a genuinely broken message reaches the dead-letter queue quickly.
    private static readonly TimeSpan[] Cooldown =
    [
        1.Seconds(),
        5.Seconds(),
        15.Seconds(),
    ];

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(c => DurableEventRoutes.IsDurableMessageType(c.MessageType)))
        {
            chain.OnAnyException()
                .RetryWithCooldown(Cooldown)
                .Then
                .MoveToErrorQueue();
        }
    }
}
