using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Models;

public abstract record DeactivatableArgs
{
    public Instant Timestamp { get; protected init; }

    /// <summary>
    /// Who is deactivating. Carried here beside <see cref="Timestamp"/> because deactivation raises a
    /// domain event, and every event requires an actor. <c>required</c> so an args object cannot be built
    /// without one — the same compile-time guarantee the event constructors give.
    /// </summary>
    public required EventActor Actor { get; init; }
}
