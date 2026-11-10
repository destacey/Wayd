namespace Wayd.Work.Application.WorkItemDependencies.Dtos;

/// <summary>The open dependencies of several teams, counted by planning health.</summary>
public sealed record TeamsDependencyHealthDto
{
    /// <summary>Each team's counts, keyed by team id; a team with no open dependency is missing.</summary>
    public IReadOnlyDictionary<Guid, TeamDependencyHealthDto> Teams { get; init; } = new Dictionary<Guid, TeamDependencyHealthDto>();

    /// <summary>
    /// The open dependencies at risk or unhealthy across all the teams, each counted once, so a dependency between
    /// two of the teams is not counted twice as summing <see cref="Teams"/> would.
    /// </summary>
    public int AtRiskOrUnhealthyCount { get; init; }
}

/// <summary>One team's open dependencies, as predecessor or successor, counted by planning health.</summary>
public sealed record TeamDependencyHealthDto
{
    /// <summary>The open dependencies with an end on the team, including those with both ends on it.</summary>
    public int OpenCount { get; init; }

    /// <summary>The open dependencies whose ends are both unplanned.</summary>
    public int AtRiskCount { get; init; }

    /// <summary>The open dependencies planned to finish after they are needed, or whose successor started first.</summary>
    public int UnhealthyCount { get; init; }
}
