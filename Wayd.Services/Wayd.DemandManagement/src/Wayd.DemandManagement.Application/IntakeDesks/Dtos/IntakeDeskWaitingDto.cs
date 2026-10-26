using NodaTime;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// The requests waiting on a desk: how many, and since when the oldest has waited.
/// </summary>
/// <remarks>
/// A desk with no members and requests waiting on it is where asks go to be forgotten, which is why this is
/// shown wherever desks are managed. Waiting means open — Proposed or Active — which is every request until
/// requests follow their desk's review workflow.
/// </remarks>
public sealed record IntakeDeskWaitingDto
{
    /// <summary>How many requests are waiting on the desk.</summary>
    public int Count { get; init; }

    /// <summary>When the oldest waiting request was submitted, or null when none is waiting.</summary>
    public Instant? OldestSubmittedOn { get; init; }
}
