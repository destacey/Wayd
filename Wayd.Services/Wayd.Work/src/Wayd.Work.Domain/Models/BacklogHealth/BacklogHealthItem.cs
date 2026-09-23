using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.BacklogHealth;

/// <summary>
/// An open work item on a team's backlog, reduced to what the health checks read.
/// </summary>
public sealed record BacklogHealthItem
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The item's position in the team's backlog, 1 being the next to be worked.
    /// </summary>
    public required int Rank { get; init; }

    public required WorkStatusCategory StatusCategory { get; init; }

    public double? StoryPoints { get; init; }

    public required Instant Created { get; init; }

    public required Instant LastModified { get; init; }

    public Instant? Activated { get; init; }

    public bool IsAssigned { get; init; }

    public bool HasParent { get; init; }

    /// <summary>
    /// The parent is done or removed while this item is still open.
    /// </summary>
    public bool IsParentClosed { get; init; }

    public bool HasProject { get; init; }

    /// <summary>
    /// The state of the sprint the item is planned in, or null when it is in none.
    /// </summary>
    public IterationState? SprintState { get; init; }

    /// <summary>
    /// Work items this item depends on. Only those also on the backlog are compared by rank.
    /// </summary>
    public IReadOnlyCollection<Guid> PredecessorIds { get; init; } = [];
}
