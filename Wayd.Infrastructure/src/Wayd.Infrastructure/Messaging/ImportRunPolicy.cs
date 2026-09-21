using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;
using Wayd.Common.Application.Imports.Commands;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// What the import runner's message is allowed to do: how long a run may take, and what happens when one fails.
/// </summary>
/// <remarks>
/// <para>
/// Needed as its own policy because <see cref="DurableEventFailurePolicy"/> is deliberately scoped to the
/// durable <em>event</em> types. Without this the import message would get no retry and no dead-lettering
/// at all, and a transient database blip mid-import would end the run for good.
/// </para>
/// <para>
/// A retry only does work if the failed attempt released its claim. The runner releases it — back to Queued
/// — only when the attempt saved nothing, and otherwise ends the run; any other delivery finds the run
/// already claimed (<c>ImportProcess.Start</c> refuses anything but Queued) and stops. So nothing is ever
/// reapplied. The retries allowed here must outnumber <c>ImportProcess.MaxAttempts</c>, or the message is
/// dead-lettered while the run still sits Queued.
/// </para>
/// </remarks>
public sealed class ImportRunPolicy : IHandlerPolicy
{
    /// <summary>
    /// How long a run may take before Wolverine cancels it.
    /// </summary>
    /// <remarks>
    /// Wolverine's default is 60 seconds, which is a request's patience applied to an operation whose size is
    /// bounded by its row cap instead. It cancels the handler's token mid-save, and the failure that surfaces
    /// is a bare <c>TaskCanceledException</c>, or a SqlException about a severe error on the current command —
    /// neither of which says anything about a timeout.
    /// <para>
    /// Deliberately longer than the command ceiling the runner raises for itself
    /// (<c>RunImportProcessCommandHandler.ImportCommandTimeout</c>): a run that genuinely will not finish
    /// should die on the database command, which names itself, rather than on the message. This is the outer
    /// bound, and the stall sweep is what settles a run that is not making progress at all.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan MaximumRunDuration = 15.Minutes();

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
            chain.ExecutionTimeoutInSeconds = (int)MaximumRunDuration.TotalSeconds;

            chain.OnAnyException()
                .RetryWithCooldown(Cooldown)
                .Then
                .MoveToErrorQueue();
        }
    }
}
