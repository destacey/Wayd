using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Activation arguments for a work process. Exists so activation and deactivation can carry an
/// <see cref="EventActor"/>: flipping a managed work process's state raises
/// <c>IntegrationStateChangedEvent</c>, and every event requires an actor.
/// </summary>
public sealed record WorkProcessActivationArgs : ActivatableArgs
{
    /// <summary>Who is flipping the state. Required, matching the event constructor it feeds.</summary>
    public required EventActor Actor { get; init; }

    public static WorkProcessActivationArgs Create(EventActor actor, Instant timestamp)
        => new() { Actor = actor, Timestamp = timestamp };
}
