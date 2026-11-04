using Wayd.Work.Application.WorkItemDependencies.Dtos;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// Where a team's dependencies, or those of every team beneath a team of teams, ran from <c>from</c> to <c>to</c>
/// (inclusive, UTC dates): which teams they ran between, how many, and how that changed over the range.
/// </summary>
/// <remarks>
/// Covers dependencies, not removed, that cross team lines, where a team in scope owns the predecessor or the
/// successor. One counts in the range when it was created before the range ended and was not closed before it
/// began. It is closed when its predecessor is done, or is removed with the successor done or removed too; a
/// removed predecessor with an open successor stays open. Teams are the items' current teams.
/// </remarks>
public sealed record TeamDependencyAnalysisDto
{
    /// <summary>The team or team of teams the report is for.</summary>
    public required WorkTeamNavigationDto Team { get; init; }

    /// <summary>The first day of the range (UTC).</summary>
    public required LocalDate From { get; init; }

    /// <summary>The last day of the range (UTC), included.</summary>
    public required LocalDate To { get; init; }

    /// <summary>Totals for the range.</summary>
    public required DependencyAnalysisSummaryDto Summary { get; init; }

    /// <summary>Each predecessor team → successor team pair, most dependencies first.</summary>
    public List<DependencyAnalysisPairDto> Pairs { get; init; } = [];

    /// <summary>The range split into calendar periods, oldest first.</summary>
    public List<DependencyAnalysisPeriodDto> Periods { get; init; } = [];

    /// <summary>
    /// Every dependency counted in the range, oldest first, with its current state and health. A closed one's
    /// health is Closed: planning health applies only while a dependency is open.
    /// </summary>
    public List<DependencyDto> Dependencies { get; init; } = [];
}

/// <summary>Totals for the range.</summary>
public sealed record DependencyAnalysisSummaryDto
{
    /// <summary>Dependencies counted in the range: open at some point within it.</summary>
    public int Dependencies { get; init; }

    /// <summary>Of those, the ones created in the range.</summary>
    public int Created { get; init; }

    /// <summary>Of those, the ones that closed in the range.</summary>
    public int Closed { get; init; }

    /// <summary>Of those, the ones still open when the range ended.</summary>
    public int OpenAtEnd { get; init; }

    /// <summary>Teams owning either end of a counted dependency, in scope or not. Items with no team add none.</summary>
    public int TeamsInvolved { get; init; }

    /// <summary>Of <see cref="TeamsInvolved"/>, the teams outside the scope: for a team, every other team.</summary>
    public int TeamsOutside { get; init; }
}

/// <summary>The dependencies running from one team's predecessors to another team's successors.</summary>
public sealed record DependencyAnalysisPairDto
{
    /// <summary>The team owning the predecessors; null when they have no team.</summary>
    public WorkTeamNavigationDto? PredecessorTeam { get; init; }

    /// <summary>The team owning the successors, waiting on the predecessors; null when they have no team.</summary>
    public WorkTeamNavigationDto? SuccessorTeam { get; init; }

    /// <summary>Whether the predecessor team is in the report's scope.</summary>
    public bool PredecessorTeamInScope { get; init; }

    /// <summary>Whether the successor team is in the report's scope.</summary>
    public bool SuccessorTeamInScope { get; init; }

    /// <summary>The pair's dependencies counted in the range: open at some point within it.</summary>
    public int Dependencies { get; init; }

    /// <summary>Of those, the ones created in the range.</summary>
    public int Created { get; init; }

    /// <summary>Of those, the ones that closed in the range.</summary>
    public int Closed { get; init; }

    /// <summary>Of those, the ones still open when the range ended.</summary>
    public int OpenAtEnd { get; init; }
}

/// <summary>One calendar period of the range.</summary>
public sealed record DependencyAnalysisPeriodDto
{
    /// <summary>The period's first day.</summary>
    public required LocalDate Start { get; init; }

    /// <summary>The period's last day, included.</summary>
    public required LocalDate End { get; init; }

    /// <summary>Dependencies created in the period.</summary>
    public int Created { get; init; }

    /// <summary>Dependencies that closed in the period.</summary>
    public int Closed { get; init; }

    /// <summary>Dependencies open at the end of the period.</summary>
    public int Open { get; init; }
}
