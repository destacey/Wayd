using NodaTime;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// The requests waiting on a desk: how many, and since when the oldest has waited.
/// </summary>
/// <remarks>
/// A desk with no members and requests waiting on it is where asks go to be forgotten, which is why this is
/// shown wherever desks are managed. Waiting means open: a status in the Proposed or Active category.
/// </remarks>
public sealed record IntakeDeskWaitingDto
{
    /// <summary>How many requests are waiting on the desk.</summary>
    public int Count { get; init; }

    /// <summary>When the oldest waiting request was submitted, or null when none is waiting.</summary>
    public Instant? OldestSubmittedOn { get; init; }

    /// <summary>How many of the waiting requests no one is assigned to: what still needs triage.</summary>
    public int UnassignedCount { get; init; }

    /// <summary>
    /// How many of the waiting requests have no score from the desk's scoring model, a score from a model the desk
    /// no longer uses included; null when the desk scores with no model.
    /// </summary>
    public int? UnscoredCount { get; init; }

    /// <summary>How many of the waiting requests have no estimate; null when the desk estimates on no scale.</summary>
    public int? UnestimatedCount { get; init; }
}
