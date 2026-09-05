using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;
using Wayd.Common.Application.Imports.Commands;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Failure policy for the import runner: retry with a bounded cooldown, then dead-letter.
/// </summary>
/// <remarks>
/// <para>
/// Needed as its own policy because <see cref="DurableEventFailurePolicy"/> is deliberately scoped to the
/// durable <em>event</em> types. Without this the import message would get no retry and no dead-lettering
/// at all, and a transient database blip mid-import would end the run for good.
/// </para>
/// <para>
/// Retrying is safe because the runner is idempotent by row status rather than by message: a redelivery
/// finds the run already claimed (<c>ImportProcess.Start</c> refuses anything but Queued) or finds fewer
/// Pending rows than last time. Nothing is reapplied.
/// </para>
/// </remarks>
public sealed class ImportFailurePolicy : IHandlerPolicy
{
    // Matches the durable event cooldown: long enough to ride out a database blip or a deadlock, short
    // enough that a genuinely broken run reaches the dead-letter queue quickly.
    private static readonly TimeSpan[] Cooldown =
    [
        1.Seconds(),
        5.Seconds(),
        15.Seconds(),
    ];

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(c => c.MessageType == typeof(RunImportProcessCommand)))
        {
            chain.OnAnyException()
                .RetryWithCooldown(Cooldown)
                .Then
                .MoveToErrorQueue();
        }
    }
}
