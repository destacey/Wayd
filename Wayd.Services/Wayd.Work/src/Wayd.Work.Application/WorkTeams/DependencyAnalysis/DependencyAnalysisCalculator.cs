using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkTeams.DependencyAnalysis;

/// <summary>A dependency to count, with when it closed.</summary>
/// <param name="ClosedOn">
/// When the dependency stopped needing planning: its predecessor was done, or was removed with the successor
/// done or removed too. Null while it is open.
/// </param>
public sealed record DependencyAnalysisItem(DependencyDto Dependency, Instant? ClosedOn);

/// <summary>
/// Counts dependencies over a date range: in total, per team pair and per calendar period. Pure: everything it
/// needs is passed in.
/// </summary>
public static class DependencyAnalysisCalculator
{
    /// <summary>
    /// Counts the dependencies open at some point from <paramref name="from"/> to <paramref name="to"/>
    /// (inclusive, days of <paramref name="calendar"/>): in total, per predecessor team → successor team pair, and
    /// per calendar period.
    /// </summary>
    /// <param name="team">The team or team of teams the report is for.</param>
    /// <param name="calendar">The days of <paramref name="team"/>, which the range and its periods are counted in.</param>
    /// <param name="scopeTeamIds">The teams the report covers; a pair's team outside them is a partner.</param>
    /// <param name="items">
    /// The dependencies to count. Any created after the range or closed before it are ignored, so a caller may
    /// pass a wider set.
    /// </param>
    public static TeamDependencyAnalysisDto Calculate(
        WorkTeamNavigationDto team,
        LocalDate from,
        LocalDate to,
        TeamReportCalendar calendar,
        IReadOnlySet<Guid> scopeTeamIds,
        IEnumerable<DependencyAnalysisItem> items)
    {
        var rangeStart = calendar.StartOf(from);
        var rangeEnd = calendar.StartOf(to.PlusDays(1));

        var counted = items
            // A link added after its predecessor was done closes as it opens, so it still counts once.
            .Select(i => i.ClosedOn < i.Dependency.CreatedOn ? i with { ClosedOn = i.Dependency.CreatedOn } : i)
            .Where(i => IsOpenDuring(i, rangeStart, rangeEnd))
            .OrderBy(i => i.Dependency.CreatedOn)
            .ThenBy(i => i.Dependency.Id)
            .ToList();

        var teams = counted
            .SelectMany(i => new[] { i.Dependency.Source.Team, i.Dependency.Target.Team })
            .OfType<WorkTeamNavigationDto>()
            .Select(t => t.Id)
            .ToHashSet();

        var summary = new DependencyAnalysisSummaryDto
        {
            Dependencies = counted.Count,
            Created = counted.Count(i => IsCreatedDuring(i, rangeStart, rangeEnd)),
            Closed = counted.Count(i => IsClosedDuring(i, rangeStart, rangeEnd)),
            OpenAtEnd = counted.Count(i => IsOpenAt(i, rangeEnd)),
            PredecessorRemoved = counted.Count(i => i.Dependency.Source.StatusCategory.Id == (int)StatusCategory.Removed),
            TeamsInvolved = teams.Count,
            TeamsOutside = teams.Count(id => !scopeTeamIds.Contains(id)),
        };

        var pairs = counted
            .GroupBy(i => (Predecessor: i.Dependency.Source.Team?.Id, Successor: i.Dependency.Target.Team?.Id))
            .Select(g =>
            {
                var first = g.First().Dependency;
                return new DependencyAnalysisPairDto
                {
                    PredecessorTeam = first.Source.Team,
                    SuccessorTeam = first.Target.Team,
                    PredecessorTeamInScope = g.Key.Predecessor is { } p && scopeTeamIds.Contains(p),
                    SuccessorTeamInScope = g.Key.Successor is { } s && scopeTeamIds.Contains(s),
                    Dependencies = g.Count(),
                    Created = g.Count(i => IsCreatedDuring(i, rangeStart, rangeEnd)),
                    Closed = g.Count(i => IsClosedDuring(i, rangeStart, rangeEnd)),
                    OpenAtEnd = g.Count(i => IsOpenAt(i, rangeEnd)),
                };
            })
            .OrderByDescending(p => p.Dependencies)
            // Among equals, pairs with a team come before those with no team.
            .ThenBy(p => p.PredecessorTeam is null || p.SuccessorTeam is null)
            .ThenBy(p => p.PredecessorTeam?.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.SuccessorTeam?.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var periods = ReportPeriods.Between(from, to)
            .Select(period =>
            {
                var start = calendar.StartOf(period.Start);
                var end = calendar.StartOf(period.End.PlusDays(1));
                return new DependencyAnalysisPeriodDto
                {
                    Start = period.Start,
                    End = period.End,
                    Created = counted.Count(i => IsCreatedDuring(i, start, end)),
                    Closed = counted.Count(i => IsClosedDuring(i, start, end)),
                    Open = counted.Count(i => IsOpenAt(i, end)),
                };
            })
            .ToList();

        return new TeamDependencyAnalysisDto
        {
            Team = team,
            From = from,
            To = to,
            TimeZone = calendar.ZoneOn(to).Id,
            Summary = summary,
            Pairs = pairs,
            Periods = periods,
            Dependencies = [.. counted.Select(i => i.Dependency)],
        };
    }

    /// <summary>Open at some point from <paramref name="start"/> up to, but not including, <paramref name="end"/>.</summary>
    private static bool IsOpenDuring(DependencyAnalysisItem item, Instant start, Instant end) =>
        item.Dependency.CreatedOn < end && (item.ClosedOn is null || item.ClosedOn >= start);

    /// <summary>Created from <paramref name="start"/> up to, but not including, <paramref name="end"/>.</summary>
    private static bool IsCreatedDuring(DependencyAnalysisItem item, Instant start, Instant end) =>
        item.Dependency.CreatedOn >= start && item.Dependency.CreatedOn < end;

    /// <summary>Closed from <paramref name="start"/> up to, but not including, <paramref name="end"/>.</summary>
    private static bool IsClosedDuring(DependencyAnalysisItem item, Instant start, Instant end) =>
        item.ClosedOn is { } closed && closed >= start && closed < end;

    /// <summary>Open at the instant: created before it, and not yet closed.</summary>
    private static bool IsOpenAt(DependencyAnalysisItem item, Instant instant) =>
        item.Dependency.CreatedOn < instant && (item.ClosedOn is null || item.ClosedOn >= instant);
}
