using Wayd.Common.Application.Dtos;
using Wayd.Work.Domain.Models.BacklogHealth;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// A team's backlog graded against a set of thresholds.
/// </summary>
public sealed record TeamBacklogHealthDto
{
    public required WorkTeamNavigationDto Team { get; init; }

    /// <summary>
    /// The thresholds the backlog was graded against: the defaults, with any the caller overrode.
    /// </summary>
    public required BacklogHealthThresholds Thresholds { get; init; }

    /// <summary>
    /// Days of history, ending yesterday (UTC), that throughput, cycle time and net flow were measured over.
    /// </summary>
    public int LookbackDays { get; init; }

    public required LocalDate From { get; init; }

    public required LocalDate To { get; init; }

    public int TotalWorkItems { get; init; }

    public double TotalStoryPoints { get; init; }

    public int ProposedWorkItems { get; init; }

    public int ActiveWorkItems { get; init; }

    /// <summary>
    /// Backlog work items the team completed in the lookback window.
    /// </summary>
    public int ItemsCompleted { get; init; }

    /// <summary>
    /// Backlog work items created for the team in the lookback window, whatever their status now.
    /// </summary>
    public int ItemsCreated { get; init; }

    /// <summary>
    /// The team's members, or null when the team is not known to the organization.
    /// </summary>
    public int? MemberCount { get; init; }

    /// <summary>
    /// The number of top-ranked work items the readiness checks looked at.
    /// </summary>
    public int ReadinessWindowWorkItems { get; init; }

    /// <summary>
    /// The cycle time, in days, an active work item is flagged beyond. Null without enough history.
    /// </summary>
    public double? AgingWipDays { get; init; }

    /// <summary>
    /// The estimate a work item is flagged as oversized above. Null without enough history.
    /// </summary>
    public double? OversizedStoryPoints { get; init; }

    public List<BacklogHealthCheckDto> Checks { get; init; } = [];

    /// <summary>
    /// Every open backlog work item, first-ranked first.
    /// </summary>
    public List<BacklogHealthWorkItemDto> WorkItems { get; init; } = [];
}

public sealed record BacklogHealthCheckDto : IMapFrom<BacklogHealthCheckResult>
{
    /// <summary>
    /// A <see cref="BacklogHealthCheck"/>.
    /// </summary>
    public required SimpleNavigationDto Check { get; init; }

    /// <summary>
    /// A <see cref="BacklogHealthOutcome"/>.
    /// </summary>
    public required SimpleNavigationDto Outcome { get; init; }

    /// <summary>
    /// A <see cref="HealthStatus"/>, or null when the check was not assessed.
    /// </summary>
    public SimpleNavigationDto? Grade { get; init; }

    /// <summary>
    /// Runway weeks, the Net Flow ratio or the WIP Load for those checks; for every other check, the
    /// percent (0 to 100) of the work items in scope that were flagged.
    /// </summary>
    public double? Value { get; init; }

    public int? Flagged { get; init; }

    public int? InScope { get; init; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<BacklogHealthCheckResult, BacklogHealthCheckDto>()
            .Map(dest => dest.Check, src => SimpleNavigationDto.FromEnum(src.Check))
            .Map(dest => dest.Outcome, src => SimpleNavigationDto.FromEnum(src.Outcome))
            .Map(dest => dest.Grade, src => src.Grade.HasValue ? SimpleNavigationDto.FromEnum(src.Grade.Value) : null);
    }
}
