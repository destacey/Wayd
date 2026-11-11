using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.Iterations.Dtos;

/// <summary>
/// The one thing worth knowing about a Scrum team's sprint today: a sprint the team started and left open past its
/// end, or else how its running sprint is tracking against its commitment.
/// </summary>
public sealed record TeamSprintSignalDto
{
    /// <summary>The sprint the signal is about: the one left open when `endedNotCompleted` is true, else the running one.</summary>
    public required NavigationDto Sprint { get; init; }

    /// <summary>
    /// Whether the team started the sprint and its end has passed without anyone completing it. A sprint the team
    /// never started follows its planned dates and is never left open.
    /// </summary>
    public bool EndedNotCompleted { get; init; }

    /// <summary>
    /// The share of the sprint's work expected done by now, from 0 to 1: along its ideal burn-down once it has a
    /// commitment, or else the share of its days gone. Null for a sprint left open, or one with nothing to measure:
    /// on its first day, or with no work in it.
    /// </summary>
    public double? ExpectedShare { get; init; }

    /// <summary>
    /// The work completed as a share of what it is measured against — the commitment, or else the sprint's work —
    /// in the team's sizing method; above 1 when more than was committed is done. Null whenever `expectedShare` is.
    /// </summary>
    public double? DoneShare { get; init; }

    /// <summary>
    /// Needs Attention for a sprint left open; otherwise the sprint's health as the sprint card shows it, and On Track
    /// while there is nothing to measure.
    /// </summary>
    public AttentionStatus Status { get; init; }
}
