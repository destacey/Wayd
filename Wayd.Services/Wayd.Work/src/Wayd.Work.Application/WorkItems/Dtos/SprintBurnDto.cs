using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// A sprint's burn-up and burn-down, worked out from work item history: its scope and completed work at the
/// commitment point, at the end of each of its days in the team's zone, and at the effective end — or now, for
/// a sprint that has not ended. Counts and estimates both, so a viewer can switch unit without refetching.
/// </summary>
public sealed record SprintBurnDto
{
    /// <summary>The sprint the burn is of.</summary>
    public Guid SprintId { get; init; }

    /// <summary>
    /// The estimate the burn is measured in: its team's sizing method on the sprint's planned start, or Count
    /// for a sprint with no team.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    /// <summary>The commitment point, where the burn and its ideal line start.</summary>
    public Instant EffectiveStart { get; init; }

    /// <summary>The effective end, where the ideal line reaches zero.</summary>
    public Instant EffectiveEnd { get; init; }

    /// <summary>The IANA zone the sprint's days are counted in.</summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// Whether a workspace holding the sprint's work has not had its history read through to the end, so the
    /// burn may be missing changes.
    /// </summary>
    public bool HistoryIncomplete { get; init; }

    /// <summary>The work committed at the commitment point, where the ideal burn-down line starts.</summary>
    public required SprintScopeMeasureDto Committed { get; init; }

    /// <summary>The readings, in time order; empty before the commitment point.</summary>
    public required List<SprintBurnPointDto> Points { get; init; }

    /// <summary>
    /// The ideal burn-down over the whole sprint, however much has passed: points at the commitment point, the
    /// start of each day in the team's zone, and the effective end, joined by straight lines. It falls only on
    /// the days the team works — its working week, less holidays and team days off — and stays flat on the rest.
    /// </summary>
    public required List<SprintIdealPointDto> Ideal { get; init; }
}

/// <summary>One reading of a sprint's burn.</summary>
public sealed record SprintBurnPointDto
{
    /// <summary>When the reading was taken.</summary>
    public Instant At { get; init; }

    /// <summary>The team's calendar day the reading closes.</summary>
    public LocalDate Day { get; init; }

    /// <summary>The work in scope at the reading.</summary>
    public required SprintScopeMeasureDto Scope { get; init; }

    /// <summary>The part of the scope in a Done- or Removed-category status at the reading.</summary>
    public required SprintScopeMeasureDto Completed { get; init; }

    /// <summary>The API shape of a domain <see cref="SprintBurnPoint"/>.</summary>
    public static SprintBurnPointDto From(SprintBurnPoint point) => new()
    {
        At = point.At,
        Day = point.Day,
        Scope = SprintScopeMeasureDto.From(point.Scope),
        Completed = SprintScopeMeasureDto.From(point.Completed),
    };
}
