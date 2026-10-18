using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// What a Kanban team completed over a span of dates, and what it had in progress at the span's end. Nothing is
/// scoped to the span, so there is no total, commitment or not-started figure.
/// </summary>
public sealed record KanbanTeamWindowMetricsDto
{
    /// <summary>The team measured.</summary>
    public required NavigationDto Team { get; init; }

    /// <summary>
    /// When the window begins: the start of its first day in the time zone of the team's operating model that day,
    /// or of the one on its last day when the team has none on the first.
    /// </summary>
    public Instant From { get; init; }

    /// <summary>
    /// When the window ends, exclusive: the start of the day after its last, in the time zone of the team's
    /// operating model on that last day.
    /// </summary>
    public Instant Until { get; init; }

    /// <summary>The IANA time zone <see cref="Until"/> is counted in: the team's on the window's last day.</summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// The estimate the window is measured in: the team's sizing method on its first day, or on its last when its
    /// operating models begin inside the window. Under Count every estimate equals its item count.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>Requirement-tier items that reached a Done-category status in the window, by done date. Removed work is not completed.</summary>
    public int CompletedWorkItems { get; init; }

    /// <summary>The completed items' estimates in <see cref="SizingMethod"/>.</summary>
    public double CompletedEstimate { get; init; }

    /// <summary>
    /// Requirement-tier items in an In Progress-category status at <see cref="InProgressAsOf"/>.
    /// </summary>
    public int InProgressWorkItems { get; init; }

    /// <summary>The in-progress items' estimates in <see cref="SizingMethod"/>, as they stood at <see cref="InProgressAsOf"/>.</summary>
    public double InProgressEstimate { get; init; }

    /// <summary>
    /// When <see cref="InProgressWorkItems"/> was read: <see cref="Until"/> for a window that has ended, now for the
    /// current one, and null for one that has not begun, which has nothing in progress.
    /// </summary>
    public Instant? InProgressAsOf { get; init; }

    /// <summary>Completed and in-progress items with no value in <see cref="SizingMethod"/>.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>Cycle-time rollup of the completed items.</summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// Whether in progress at the end of a past window may be short, because a workspace holding the team's work
    /// has not had its history read through to the end.
    /// </summary>
    public bool IsHistoryIncomplete { get; init; }
}
