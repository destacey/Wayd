using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkTeams.Allocation;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// Where a team's completed work went from <see cref="From"/> to <see cref="To"/>, grouped by one dimension.
/// For a team of teams it covers every team beneath it, each placed where it sat on the day the work was done.
/// </summary>
/// <remarks>
/// <see cref="AllocationTeamRowDto.Cells"/> and <see cref="AllocationPeriodDto.Values"/> line up with
/// <see cref="Groups"/> by index.
/// </remarks>
public sealed record TeamAllocationDto
{
    public required WorkTeamNavigationDto Team { get; init; }
    public required LocalDate From { get; init; }
    public required LocalDate To { get; init; }
    public required AllocationSummaryDto Summary { get; init; }
    public List<AllocationGroupDto> Groups { get; init; } = [];

    /// <summary>The team hierarchy, pre-ordered from the requested team, each placed under its latest parent in the window.</summary>
    public List<AllocationTeamRowDto> Teams { get; init; } = [];

    public List<AllocationPeriodDto> Periods { get; init; } = [];
}

public sealed record AllocationSummaryDto
{
    public int ItemsCompleted { get; init; }

    /// <summary>Completed items from teams that sized in story points on the day each was done.</summary>
    public int ItemsInPointSizedTeams { get; init; }

    /// <summary>Of <see cref="ItemsInPointSizedTeams"/>, those with an estimate above zero.</summary>
    public int EstimatedItems { get; init; }

    /// <summary>Estimated story points; filled-in points are reported separately.</summary>
    public double StoryPoints { get; init; }

    public int FilledItems { get; init; }
    public double FilledStoryPoints { get; init; }

    /// <summary>Teams (not teams of teams) that completed work in the window.</summary>
    public int TeamsIncluded { get; init; }

    /// <summary>Teams whose work the measure leaves out, such as count-sized teams under story points.</summary>
    public List<AllocationTeamReferenceDto> ExcludedTeams { get; init; } = [];

    /// <summary>Items with no project in their ancestry.</summary>
    public int NoProjectItems { get; init; }

    /// <summary>Percent of the measured total with no project.</summary>
    public double NoProjectShare { get; init; }
}

public sealed record AllocationTeamReferenceDto(Guid Id, string Code, string Name);

public sealed record AllocationGroupDto
{
    /// <summary>Stable within a response; cells and periods refer to groups by position, not by this.</summary>
    public required string Id { get; init; }

    public required AllocationGroupKind Kind { get; init; }

    /// <summary>The portfolio, program, project or theme's id; null for work types and gap groups.</summary>
    public Guid? RecordId { get; init; }

    public string? RecordKey { get; init; }

    public required string Name { get; init; }

    /// <summary>For a program or project group: the portfolio it belongs to.</summary>
    public NavigationDto? Portfolio { get; init; }

    /// <summary>For a project group: its program, when it has one.</summary>
    public NavigationDto? Program { get; init; }

    /// <summary>Keys of the projects whose work landed in this group, ordered.</summary>
    public List<string> ProjectKeys { get; init; } = [];

    public int ProgramCount { get; init; }

    public double Items { get; init; }
    public double StoryPoints { get; init; }
    public double FilledStoryPoints { get; init; }

    /// <summary>The group's total in the requested measure.</summary>
    public double Value { get; init; }

    /// <summary>Percent of the measured total.</summary>
    public double Share { get; init; }

    /// <summary>Work type groups only: the part of <see cref="Value"/> with no project.</summary>
    public double? NoProjectValue { get; init; }

    /// <summary>Work type groups only: percent of this group's value with no project.</summary>
    public double? NoProjectShare { get; init; }

    public AllocationContributorDto? LargestContributor { get; init; }
}

public sealed record AllocationContributorDto(Guid TeamId, string Code, string Name, double Value, double Items);

public sealed record AllocationTeamRowDto
{
    public required Guid TeamId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required bool IsTeamOfTeams { get; init; }
    public Guid? ParentId { get; init; }
    public int Level { get; init; }

    /// <summary>True when the row completed work but the measure counts none of it.</summary>
    public bool Excluded { get; init; }

    public string? ExcludedReason { get; init; }

    public int Items { get; init; }
    public double StoryPoints { get; init; }
    public double Value { get; init; }
    public List<AllocationCellDto> Cells { get; init; } = [];

    /// <summary>Work type dimension only: percent of this row's value with no project.</summary>
    public double? NoProjectShare { get; init; }
}

/// <param name="Share">Percent of the row's measured total.</param>
public sealed record AllocationCellDto(double Value, double Share, double Items);

public sealed record AllocationPeriodDto
{
    public required LocalDate Start { get; init; }
    public required LocalDate End { get; init; }
    public int Items { get; init; }
    public double Value { get; init; }
    public List<double> Values { get; init; } = [];

    /// <summary>Percent of the period's measured total, per group.</summary>
    public List<double> Shares { get; init; } = [];

    public double NoProjectShare { get; init; }
}
