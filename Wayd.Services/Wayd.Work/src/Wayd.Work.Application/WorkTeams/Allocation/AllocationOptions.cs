namespace Wayd.Work.Application.WorkTeams.Allocation;

/// <summary>What completed work is grouped by.</summary>
public enum AllocationDimension
{
    Portfolio = 0,
    Program = 1,
    Project = 2,
    StrategicTheme = 3,
    WorkType = 4,
}

/// <summary>How each completed work item is weighed.</summary>
public enum AllocationMeasure
{
    /// <summary>Every item counts as one, in every team, as a team that sizes by count does.</summary>
    Count = 0,

    /// <summary>Story points, only from teams that sized in story points on the day the work was done.</summary>
    StoryPoints = 1,

    /// <summary>
    /// Each team's split measured in its own sizing method (story points, effort, size or count, as it sized
    /// on the day the work was done), then combined by share of completed items, so estimates are never added
    /// across teams whose scales differ or across one team's units.
    /// </summary>
    TeamEffort = 2,
}

/// <summary>What an estimate-based measure does with a work item that has no estimate in its team's unit.</summary>
public enum UnestimatedHandling
{
    Exclude = 0,

    /// <summary>The average estimate per estimated item of the same team, sizing method and work type in the window.</summary>
    TeamAverage = 1,
}

/// <summary>How work on a project with several effective themes is credited.</summary>
public enum ThemeCounting
{
    /// <summary>Divided evenly, so the dimension adds up to 100% like the others.</summary>
    SplitEvenly = 0,

    /// <summary>Counted in full under each theme; shares then add up to more than 100%.</summary>
    CountFully = 1,
}

public enum AllocationGroupKind
{
    Record = 0,

    /// <summary>Neither the work item nor its parents link to a project.</summary>
    NoProject = 1,

    /// <summary>The work is on a project, but the project has no program or no theme.</summary>
    MissingLevel = 2,
}

public sealed record AllocationOptions(
    AllocationDimension Dimension,
    AllocationMeasure Measure,
    UnestimatedHandling Unestimated,
    ThemeCounting ThemeCounting);
