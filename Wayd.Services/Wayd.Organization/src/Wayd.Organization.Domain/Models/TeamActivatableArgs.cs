using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;

namespace Wayd.Organization.Domain.Models;

/// <summary>
/// Activation arguments for teams. Exists so activation can carry an <see cref="EventActor"/>: activating a
/// team raises a domain event, and every event requires an actor. Most <c>IActivatable</c> implementors
/// raise no events and keep activating on a bare <see cref="Instant"/>.
/// </summary>
public sealed record TeamActivatableArgs : ActivatableArgs
{
    /// <summary>Who is activating. Required, matching the event constructor it feeds.</summary>
    public required EventActor Actor { get; init; }

    public static TeamActivatableArgs Create(EventActor actor, Instant timestamp)
        => new() { Actor = actor, Timestamp = timestamp };
}
