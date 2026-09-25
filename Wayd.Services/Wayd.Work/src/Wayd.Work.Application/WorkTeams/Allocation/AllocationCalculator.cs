using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkTeams.Allocation;

/// <param name="ProjectId">The item's own project, or the one inherited from its parent.</param>
public sealed record AllocationWorkItem(Guid Id, Guid TeamId, string WorkTypeName, LocalDate DoneOn, Guid? ProjectId, double? StoryPoints);

/// <summary>
/// Groups completed work items by one dimension in one measure. Pure: everything it needs is passed in.
/// </summary>
public static class AllocationCalculator
{
    public const string NoProjectGroupId = "no-project";
    public const string MissingLevelGroupId = "missing-level";

    public static TeamAllocationDto Calculate(
        WorkTeamNavigationDto team,
        LocalDate from,
        LocalDate to,
        TeamStructure structure,
        IReadOnlyList<AllocationWorkItem> workItems,
        IReadOnlyDictionary<Guid, ProjectClassification> projects,
        AllocationOptions options)
    {
        var teamsById = structure.Teams.ToDictionary(t => t.Id);
        var parentEdges = structure.Memberships.ToLookup(m => m.ChildId);
        var sizing = structure.SizingPeriods.ToLookup(p => p.TeamId);

        var placed = new List<PlacedItem>(workItems.Count);
        foreach (var item in workItems)
        {
            // Every placed item must land in a period, and the periods cover only the window.
            if (item.DoneOn < from || item.DoneOn > to)
                continue;

            // Work done while the team sat outside this hierarchy is not this hierarchy's work.
            var path = PathToRoot(item.TeamId, item.DoneOn, structure.RootId, parentEdges);
            if (path is null)
                continue;

            // A team with no operating model on the day is treated as count-sized.
            var usesPoints = sizing[item.TeamId].FirstOrDefault(p => p.IncludesDate(item.DoneOn))?.UsesStoryPoints ?? false;
            var project = item.ProjectId is { } projectId && projects.TryGetValue(projectId, out var found) ? found : null;
            placed.Add(new PlacedItem(item, path, usesPoints, item.StoryPoints is > 0 ? item.StoryPoints : null, project));
        }

        Measure(placed, options);

        var buckets = Buckets(from, to);
        var groups = new Dictionary<string, GroupAccumulator>();
        var rows = new Dictionary<Guid, RowAccumulator>();
        var periods = buckets.Select(b => new PeriodAccumulator(b.Start, b.End)).ToList();

        foreach (var p in placed)
        {
            var value = p.Value ?? 0;
            var period = periods.First(b => b.Start <= p.Item.DoneOn && p.Item.DoneOn <= b.End);
            period.Add(p, value);

            foreach (var teamId in p.Path)
                rows.GetOrAdd(teamId, _ => new RowAccumulator()).Add(p, value);

            foreach (var (groupId, weight) in GroupsOf(p, options))
            {
                var group = groups.GetOrAdd(groupId, id => GroupAccumulator.For(id, p, options.Dimension));
                group.Add(p, weight, value);
                period.AddToGroup(groupId, weight * value);
                foreach (var teamId in p.Path)
                    rows[teamId].AddToGroup(groupId, weight * value, weight);
            }
        }

        var total = placed.Sum(p => p.Value ?? 0);
        var orderedGroups = groups.Values
            .OrderBy(g => g.Kind switch { AllocationGroupKind.Record => 0, AllocationGroupKind.MissingLevel => 1, _ => 2 })
            .ThenByDescending(g => g.Value)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var teamRows = TeamRows(structure, teamsById, rows, orderedGroups, options);

        return new TeamAllocationDto
        {
            Team = team,
            From = from,
            To = to,
            Summary = new AllocationSummaryDto
            {
                ItemsCompleted = placed.Count,
                ItemsInPointSizedTeams = placed.Count(p => p.UsesPoints),
                EstimatedItems = placed.Count(p => p.UsesPoints && p.Estimate is not null),
                StoryPoints = placed.Where(p => p.UsesPoints).Sum(p => p.Estimate ?? 0),
                FilledItems = placed.Count(p => p.Filled is not null),
                FilledStoryPoints = placed.Sum(p => p.Filled ?? 0),
                TeamsIncluded = placed.Select(p => p.Item.TeamId).Distinct().Count(),
                ExcludedTeams = [.. teamRows
                    .Where(r => r.Excluded && !r.IsTeamOfTeams)
                    .Select(r => new AllocationTeamReferenceDto(r.TeamId, r.Code, r.Name))],
                NoProjectItems = placed.Count(p => p.Project is null),
                NoProjectShare = Percent(placed.Where(p => p.Project is null).Sum(p => p.Value ?? 0), total),
            },
            Groups = [.. orderedGroups.Select(g => g.ToDto(total, teamsById, options.Dimension))],
            Teams = teamRows,
            Periods = [.. periods.Select(p => p.ToDto(orderedGroups))],
        };
    }

    /// <returns>The team and each parent up to the root on <paramref name="date"/>; null when it was not under the root that day.</returns>
    private static List<Guid>? PathToRoot(Guid teamId, LocalDate date, Guid rootId, ILookup<Guid, TeamStructureMembership> parentEdges)
    {
        var path = new List<Guid> { teamId };
        var current = teamId;
        while (current != rootId)
        {
            var edge = parentEdges[current].FirstOrDefault(e => e.IncludesDate(date));
            if (edge is null || path.Contains(edge.ParentId))
                return null;

            current = edge.ParentId;
            path.Add(current);
        }

        return path;
    }

    private static void Measure(List<PlacedItem> placed, AllocationOptions options)
    {
        var fillByTeamAndType = new Dictionary<(Guid, string), double>();
        var fillByTeam = new Dictionary<Guid, double>();
        if (options.Unestimated == UnestimatedHandling.TeamAverage)
        {
            var estimated = placed.Where(p => p.UsesPoints && p.Estimate is not null).ToList();
            fillByTeamAndType = estimated
                .GroupBy(p => (p.Item.TeamId, TypeKey(p.Item.WorkTypeName)))
                .ToDictionary(g => g.Key, g => g.Average(p => p.Estimate!.Value));
            fillByTeam = estimated
                .GroupBy(p => p.Item.TeamId)
                .ToDictionary(g => g.Key, g => g.Average(p => p.Estimate!.Value));
        }

        double? Points(PlacedItem p)
        {
            if (!p.UsesPoints)
                return null;
            if (p.Estimate is { } estimate)
                return estimate;

            // Averages are per team because points are relative within a team, and per work type because
            // bugs and tasks are often left unpointed and a story average would inflate them.
            if (fillByTeamAndType.TryGetValue((p.Item.TeamId, TypeKey(p.Item.WorkTypeName)), out var fill)
                || fillByTeam.TryGetValue(p.Item.TeamId, out fill))
            {
                p.Filled = fill;
                return fill;
            }

            return null;
        }

        switch (options.Measure)
        {
            case AllocationMeasure.Count:
                foreach (var p in placed)
                    p.Value = 1;
                break;

            case AllocationMeasure.StoryPoints:
                foreach (var p in placed)
                    p.Value = Points(p);
                break;

            case AllocationMeasure.TeamEffort:
                foreach (var teamItems in placed.GroupBy(p => p.Item.TeamId))
                {
                    var own = teamItems.Select(p => (Item: p, Value: p.UsesPoints ? Points(p) : 1)).ToList();
                    var sum = own.Sum(x => x.Value ?? 0);

                    // A point-sized team with nothing estimated still did the work; its items weigh equally.
                    if (sum <= 0)
                    {
                        own = [.. teamItems.Select(p => (Item: p, Value: (double?)1))];
                        sum = own.Count;
                    }

                    var teamWeight = (double)own.Count / placed.Count * 100;
                    foreach (var (item, value) in own)
                        item.Value = value is null ? null : teamWeight * value.Value / sum;
                }
                break;
        }
    }

    private static IEnumerable<(string GroupId, double Weight)> GroupsOf(PlacedItem p, AllocationOptions options)
    {
        var project = p.Project;
        switch (options.Dimension)
        {
            case AllocationDimension.WorkType:
                yield return ($"type:{TypeKey(p.Item.WorkTypeName)}", 1);
                yield break;

            case AllocationDimension.Portfolio when project is not null:
                yield return ($"portfolio:{project.Portfolio.Id}", 1);
                yield break;

            case AllocationDimension.Program when project is not null:
                yield return project.Program is null ? (MissingLevelGroupId, 1) : ($"program:{project.Program.Id}", 1);
                yield break;

            case AllocationDimension.Project when project is not null:
                yield return ($"project:{project.ProjectId}", 1);
                yield break;

            case AllocationDimension.StrategicTheme when project is not null:
                if (project.Themes.Count == 0)
                {
                    yield return (MissingLevelGroupId, 1);
                    yield break;
                }

                var weight = options.ThemeCounting == ThemeCounting.SplitEvenly ? 1.0 / project.Themes.Count : 1;
                foreach (var theme in project.Themes)
                    yield return ($"theme:{theme.Id}", weight);
                yield break;

            default:
                yield return (NoProjectGroupId, 1);
                yield break;
        }
    }

    private static List<AllocationTeamRowDto> TeamRows(
        TeamStructure structure,
        Dictionary<Guid, TeamStructureTeam> teamsById,
        Dictionary<Guid, RowAccumulator> rows,
        List<GroupAccumulator> groups,
        AllocationOptions options)
    {
        // A team that moved inside the window is drawn under its latest parent; its work still rolls up to
        // whichever parent it had on the day each item was done.
        var latestParent = structure.Memberships
            .GroupBy(m => m.ChildId)
            .ToDictionary(g => g.Key, g => g.MaxBy(m => m.Start)!.ParentId);
        var children = latestParent
            .Where(x => x.Key != structure.RootId && teamsById.ContainsKey(x.Key))
            .ToLookup(x => x.Value, x => teamsById[x.Key]);

        var result = new List<AllocationTeamRowDto>(teamsById.Count);
        var visited = new HashSet<Guid>();
        var pending = new Stack<(TeamStructureTeam Team, Guid? ParentId, int Level)>();
        if (teamsById.TryGetValue(structure.RootId, out var root))
            pending.Push((root, null, 0));

        while (pending.TryPop(out var next))
        {
            if (!visited.Add(next.Team.Id))
                continue;

            var row = rows.GetValueOrDefault(next.Team.Id) ?? new RowAccumulator();
            var excluded = options.Measure == AllocationMeasure.StoryPoints && row.Items > 0 && row.PointSizedItems == 0;

            result.Add(new AllocationTeamRowDto
            {
                TeamId = next.Team.Id,
                Code = next.Team.Code,
                Name = next.Team.Name,
                IsTeamOfTeams = next.Team.Type == TeamType.TeamOfTeams,
                ParentId = next.ParentId,
                Level = next.Level,
                Excluded = excluded,
                ExcludedReason = excluded ? "Sizes by count, so its work has no story points." : null,
                Items = row.Items,
                StoryPoints = row.StoryPoints,
                Value = row.Value,
                Cells = [.. groups.Select(g =>
                {
                    var (value, items) = row.ByGroup.GetValueOrDefault(g.Id);
                    return new AllocationCellDto(value, Percent(value, row.Value), items);
                })],
                NoProjectShare = options.Dimension == AllocationDimension.WorkType ? Percent(row.NoProjectValue, row.Value) : null,
            });

            // Pushed in reverse so they pop in name order.
            foreach (var child in children[next.Team.Id].OrderByDescending(t => t.Name, StringComparer.OrdinalIgnoreCase))
                pending.Push((child, next.Team.Id, next.Level + 1));
        }

        return result;
    }

    /// <summary>
    /// Calendar buckets: weeks up to a month, two weeks up to six months, otherwise calendar months. The team
    /// of teams' own teams may run different iteration cadences, so iterations cannot be the bucket.
    /// </summary>
    private static List<(LocalDate Start, LocalDate End)> Buckets(LocalDate from, LocalDate to)
    {
        var buckets = new List<(LocalDate, LocalDate)>();
        var days = Period.Between(from, to, PeriodUnits.Days).Days + 1;

        if (days > 184)
        {
            for (var start = from; start <= to;)
            {
                var monthEnd = start.With(DateAdjusters.EndOfMonth);
                var end = monthEnd < to ? monthEnd : to;
                buckets.Add((start, end));
                start = end.PlusDays(1);
            }

            return buckets;
        }

        var size = days <= 31 ? 7 : 14;
        for (var start = from; start <= to; start = start.PlusDays(size))
        {
            var end = start.PlusDays(size - 1);
            buckets.Add((start, end < to ? end : to));
        }

        return buckets;
    }

    private static string TypeKey(string workTypeName) => workTypeName.Trim().ToUpperInvariant();

    private static double Percent(double part, double whole) => whole > 0 ? part / whole * 100 : 0;

    private static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> create)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = create(key);
            dictionary[key] = value;
        }

        return value;
    }

    private sealed class PlacedItem(AllocationWorkItem item, List<Guid> path, bool usesPoints, double? estimate, ProjectClassification? project)
    {
        public AllocationWorkItem Item { get; } = item;

        /// <summary>The item's team, then each parent up to the root, as of the day it was done.</summary>
        public List<Guid> Path { get; } = path;

        public bool UsesPoints { get; } = usesPoints;
        public double? Estimate { get; } = estimate;
        public ProjectClassification? Project { get; } = project;

        /// <summary>Points filled in from the team average; null when the item was estimated or not filled.</summary>
        public double? Filled { get; set; }

        /// <summary>Null when the measure leaves the item out.</summary>
        public double? Value { get; set; }
    }

    private sealed class GroupAccumulator
    {
        private readonly SortedSet<string> _projectKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Guid> _programIds = [];
        private readonly Dictionary<Guid, (double Value, double Items)> _byTeam = [];

        public required string Id { get; init; }
        public required AllocationGroupKind Kind { get; init; }
        public Guid? RecordId { get; init; }
        public string? RecordKey { get; init; }
        public required string Name { get; init; }
        public PpmRecordReference? Portfolio { get; init; }
        public PpmRecordReference? Program { get; init; }

        public double Items { get; private set; }
        public double StoryPoints { get; private set; }
        public double FilledStoryPoints { get; private set; }
        public double Value { get; private set; }
        public double NoProjectValue { get; private set; }

        public static GroupAccumulator For(string id, PlacedItem first, AllocationDimension dimension)
        {
            if (id == NoProjectGroupId)
                return new GroupAccumulator { Id = id, Kind = AllocationGroupKind.NoProject, Name = "No project" };

            if (id == MissingLevelGroupId)
            {
                var name = dimension == AllocationDimension.Program ? "No program" : "No theme";
                return new GroupAccumulator { Id = id, Kind = AllocationGroupKind.MissingLevel, Name = name };
            }

            var project = first.Project;
            return dimension switch
            {
                AllocationDimension.Portfolio => Record(id, project!.Portfolio),
                AllocationDimension.Program => Record(id, project!.Program!, project.Portfolio),
                AllocationDimension.Project => new GroupAccumulator
                {
                    Id = id,
                    Kind = AllocationGroupKind.Record,
                    RecordId = project!.ProjectId,
                    RecordKey = project.ProjectKey,
                    Name = project.ProjectName,
                    Portfolio = project.Portfolio,
                    Program = project.Program,
                },
                AllocationDimension.StrategicTheme => Record(id, project!.Themes.Single(t => id == $"theme:{t.Id}")),
                _ => new GroupAccumulator { Id = id, Kind = AllocationGroupKind.Record, Name = first.Item.WorkTypeName.Trim() },
            };
        }

        private static GroupAccumulator Record(string id, PpmRecordReference record, PpmRecordReference? portfolio = null) => new()
        {
            Id = id,
            Kind = AllocationGroupKind.Record,
            RecordId = record.Id,
            RecordKey = record.Key.ToString(),
            Name = record.Name,
            Portfolio = portfolio,
        };

        public void Add(PlacedItem p, double weight, double value)
        {
            Items += weight;
            if (p.UsesPoints && p.Estimate is { } estimate)
                StoryPoints += weight * estimate;
            FilledStoryPoints += weight * (p.Filled ?? 0);
            Value += weight * value;
            if (p.Project is null)
                NoProjectValue += weight * value;

            if (p.Project is not null)
            {
                _projectKeys.Add(p.Project.ProjectKey);
                if (p.Project.Program is not null)
                    _programIds.Add(p.Project.Program.Id);
            }

            var (teamValue, teamItems) = _byTeam.GetValueOrDefault(p.Item.TeamId);
            _byTeam[p.Item.TeamId] = (teamValue + weight * value, teamItems + weight);
        }

        public AllocationGroupDto ToDto(double total, Dictionary<Guid, TeamStructureTeam> teamsById, AllocationDimension dimension)
        {
            var largest = _byTeam
                .Where(x => teamsById.ContainsKey(x.Key))
                .OrderByDescending(x => x.Value.Value)
                .ThenByDescending(x => x.Value.Items)
                .Select(x => (KeyValuePair<Guid, (double Value, double Items)>?)x)
                .FirstOrDefault();

            return new AllocationGroupDto
            {
                Id = Id,
                Kind = Kind,
                RecordId = RecordId,
                RecordKey = RecordKey,
                Name = Name,
                Portfolio = Portfolio is null ? null : NavigationDto.Create(Portfolio.Id, Portfolio.Key, Portfolio.Name),
                Program = Program is null ? null : NavigationDto.Create(Program.Id, Program.Key, Program.Name),
                ProjectKeys = [.. _projectKeys],
                ProgramCount = _programIds.Count,
                Items = Items,
                StoryPoints = StoryPoints,
                FilledStoryPoints = FilledStoryPoints,
                Value = Value,
                Share = Percent(Value, total),
                NoProjectValue = dimension == AllocationDimension.WorkType ? NoProjectValue : null,
                NoProjectShare = dimension == AllocationDimension.WorkType ? Percent(NoProjectValue, Value) : null,
                LargestContributor = largest is { } l
                    ? new AllocationContributorDto(l.Key, teamsById[l.Key].Code, teamsById[l.Key].Name, l.Value.Value, l.Value.Items)
                    : null,
            };
        }
    }

    private sealed class RowAccumulator
    {
        public int Items { get; private set; }
        public int PointSizedItems { get; private set; }
        public double StoryPoints { get; private set; }
        public double Value { get; private set; }
        public double NoProjectValue { get; private set; }
        public Dictionary<string, (double Value, double Items)> ByGroup { get; } = [];

        public void Add(PlacedItem p, double value)
        {
            Items++;
            if (p.UsesPoints)
            {
                PointSizedItems++;
                StoryPoints += p.Estimate ?? 0;
            }

            Value += value;
            if (p.Project is null)
                NoProjectValue += value;
        }

        public void AddToGroup(string groupId, double value, double items)
        {
            var (currentValue, currentItems) = ByGroup.GetValueOrDefault(groupId);
            ByGroup[groupId] = (currentValue + value, currentItems + items);
        }
    }

    private sealed class PeriodAccumulator(LocalDate start, LocalDate end)
    {
        private readonly Dictionary<string, double> _byGroup = [];

        public LocalDate Start { get; } = start;
        public LocalDate End { get; } = end;
        public int Items { get; private set; }
        public double Value { get; private set; }
        public double NoProjectValue { get; private set; }

        public void Add(PlacedItem p, double value)
        {
            Items++;
            Value += value;
            if (p.Project is null)
                NoProjectValue += value;
        }

        public void AddToGroup(string groupId, double value) =>
            _byGroup[groupId] = _byGroup.GetValueOrDefault(groupId) + value;

        public AllocationPeriodDto ToDto(List<GroupAccumulator> groups) => new()
        {
            Start = Start,
            End = End,
            Items = Items,
            Value = Value,
            Values = [.. groups.Select(g => _byGroup.GetValueOrDefault(g.Id))],
            Shares = [.. groups.Select(g => Percent(_byGroup.GetValueOrDefault(g.Id), Value))],
            NoProjectShare = Percent(NoProjectValue, Value),
        };
    }
}
