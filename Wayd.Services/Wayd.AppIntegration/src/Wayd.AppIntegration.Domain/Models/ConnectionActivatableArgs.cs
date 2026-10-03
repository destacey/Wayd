using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Domain.Models;

/// <summary>
/// Activation and deactivation arguments for connections, carrying the <see cref="EventActor"/> the
/// state-change events require.
/// </summary>
public sealed record ConnectionActivatableArgs : ActivatableArgs
{
    /// <summary>Who is activating or deactivating. Required, matching the event constructors it feeds.</summary>
    public required EventActor Actor { get; init; }

    public static ConnectionActivatableArgs Create(EventActor actor, Instant timestamp)
        => new() { Actor = actor, Timestamp = timestamp };
}
