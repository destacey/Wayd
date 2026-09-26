using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mono.Cecil;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Organization.Domain.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Work.Domain.Models;
using StrategicTheme = Wayd.StrategicManagement.Domain.Models.StrategicTheme;
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Keeps an evented aggregate's history complete: nothing may change its state without raising an event.
/// </summary>
/// <remarks>
/// Every event is recorded in the activity log, and that log is the aggregate's history. One mutation that
/// raises nothing leaves the history silently incomplete, and a replay of it silently wrong — and nothing
/// else notices, because the row still saves. So an evented aggregate's public methods must raise when they
/// change state, and nothing may write its table in bulk, around the aggregate altogether.
/// <para>
/// Precision matters more than recall here. The method check follows each public method through the domain
/// code it calls and fails only when it finds a write with no <c>AddDomainEvent</c> anywhere on the way; a
/// method that raises on one branch and not another passes. The set-based check reads source rather than
/// running it. Both are meant to catch the common mistake, a new unevented mutator or a new bulk write,
/// without flagging code that is already right.
/// </para>
/// </remarks>
public partial class EventCoverageTests
{
    /// <summary>
    /// The aggregates whose history is their events. An aggregate joins this list the moment it raises its
    /// first event — <see cref="EventedAggregates_AreExactlyTheTypesThatRaiseEvents"/> fails until it does —
    /// and from then on every public mutator on it must raise too.
    /// </summary>
    private static readonly HashSet<Type> EventedAggregates =
    [
        // Common
        typeof(ScoringModel),
        typeof(StatusWorkflow),
        typeof(SystemSettingsSection),
        typeof(WorkflowAssignment),

        // Organization
        typeof(Team),
        typeof(TeamOfTeams),

        // Planning
        typeof(Iteration),
        typeof(PlanningInterval),
        typeof(PlanningIntervalObjective),
        typeof(Risk),

        // Product Management
        typeof(Deployment),
        typeof(DeploymentEnvironment),
        typeof(Product),
        typeof(Release),
        typeof(ReleasePackage),
        typeof(Version),

        // Project Portfolio Management
        typeof(Program),
        typeof(Project),
        typeof(ProjectPortfolio),
        typeof(StrategicInitiative),

        // Strategic Management
        typeof(StrategicTheme),

        // Work
        typeof(WorkIteration),
        typeof(WorkProcess),
    ];

    /// <summary>
    /// Public mutators on an evented aggregate that change state without raising, each with the reason.
    /// Keyed as the failure message names them. An entry whose method is gone, or now raises, fails
    /// <see cref="UneventedMutators_AreStillUnevented"/>, so the list cannot outlive what it excuses.
    /// </summary>
    private static readonly Dictionary<string, string> UneventedMutators = new()
    {
        ["StatusTrackedEntity.DrainStatusTransitions()"] =
            "Hands the pending transition rows to BaseDbContext to insert; the saved record is unchanged.",
        ["WorkIteration.ctor(ISimpleIteration, Instant)"] =
            "Builds the Work copy when the owning Iteration's creation event arrives; that event is the history.",

        // Known gaps: each changes state a reader of the history would expect to see.
        ["StatusTrackedEntity.SwitchWorkflow(StatusRemap, EventActor, Instant, String)"] = "Gap: #896.",
        ["DeploymentEnvironment.Update(String, Int32)"] = "Gap: #897.",
        ["DeploymentEnvironment.Activate()"] = "Gap: #897.",
        ["Team.SetOperatingModel(LocalDate, Methodology, SizingMethod)"] = "Gap: #813.",
        ["Team.RemoveOperatingModel(Guid)"] = "Gap: #813.",
        ["BaseTeam.AddMember(Employee, IReadOnlyList<Guid>)"] = "Gap: #813.",
        ["BaseTeam.AddMember(Employee, Guid)"] = "Gap: #813.",
        ["BaseTeam.UpdateMemberRoles(Employee, IReadOnlyList<Guid>)"] = "Gap: #813.",
        ["BaseTeam.RemoveMember(Guid)"] = "Gap: #813.",
        ["BaseTeam.AddTeamMembership(TeamOfTeams, MembershipDateRange, Instant)"] = "Gap: #813.",
        ["BaseTeam.UpdateTeamMembership(Guid, MembershipDateRange, Instant)"] = "Gap: #813.",
        ["BaseTeam.RemoveTeamMembership(Guid)"] = "Gap: #813.",

        // Undecided (#816): whether these changes are part of the aggregate's history at all.
        ["ProjectPortfolio.MoveProjectRanks(PpmActor, IReadOnlyList<Guid>, Nullable<Guid>, Nullable<Guid>)"] = "Ranking: #816.",
        ["ProjectPortfolio.RebalanceRanks(PpmActor)"] = "Ranking: #816.",
        ["Project.CreateTask(Int32, String, String, ProjectTaskType, TaskStatus, TaskPriority, Progress, Guid, FlexibleDateRange, Nullable<LocalDate>, Nullable<Decimal>, Dictionary<TaskRole, HashSet<Guid>>)"] = "Project tasks: #816.",
        ["Project.ChangeTaskPlacement(Guid, Guid, Nullable<Int32>)"] = "Project tasks: #816.",
        ["Project.DeleteTask(Guid)"] = "Project tasks: #816.",
        ["Project.LinkTaskParents()"] = "Project tasks: #816.",
        ["Project.UpdateTaskDates(Guid, FlexibleDateRange, Nullable<LocalDate>, Boolean)"] = "Project tasks: #816.",
        ["Project.UpdateStageDates(Guid, FlexibleDateRange)"] = "Project tasks: #816.",
        ["Project.RecalculateAncestorsForTask(ProjectTask)"] = "Project tasks: #816.",
        ["WorkProcess.Create(String, String, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.CreateExternal(String, String, Guid, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.Update(String, String, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.AddWorkType(Int32, Guid, Boolean, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.ActivateWorkType(Int32, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.DeactivateWorkType(Int32, Instant)"] = "Synced process configuration: #816.",
        ["WorkProcess.ChangeWorkTypeWorkflow(Int32, Guid, Instant)"] = "Synced process configuration: #816.",
    };

    /// <summary>
    /// Migrations that write an evented aggregate's table on purpose, by file name, each with the reason —
    /// typically one that writes the matching activity log entries itself, as the baseline backfills do.
    /// </summary>
    private static readonly Dictionary<string, string> SetBasedWriteMigrations = [];

    private static readonly Lazy<DomainMethodAnalysis> Analysis = new(DomainMethodAnalysis.Load);

    public static TheoryData<string> EventedAggregateNames() => new(EventedAggregates.Select(t => t.FullName!).Order());

    [Fact]
    public void EventedAggregates_AreExactlyTheTypesThatRaiseEvents()
    {
        // Arrange
        var analysis = Analysis.Value;
        var listed = EventedAggregates.Select(analysis.Definition).ToList();

        // Act
        var raisers = analysis.DomainTypes()
            .Where(t => t.Methods.Any(DomainMethodAnalysis.RaisesDirectly))
            .Select(DomainMethodAnalysis.Owner)
            .Distinct()
            .ToList();

        // Assert — a raiser may be a base class, covered by the aggregates deriving from it
        var unlisted = raisers
            .Where(r => !listed.Any(a => DomainMethodAnalysis.DerivesFrom(a, r)))
            .Select(r => r.FullName);
        var stale = listed
            .Where(a => !raisers.Any(r => DomainMethodAnalysis.DerivesFrom(a, r)))
            .Select(a => a.FullName);

        Listed(unlisted).Should().BeEmpty(
            "a type that raises an event is an evented aggregate, and every public mutator on it must raise " +
            "too. Add it to EventedAggregates");
        Listed(stale).Should().BeEmpty(
            "an aggregate on EventedAggregates raises no event, so the list no longer says which histories are " +
            "complete. Remove it, or find where its events went");
    }

    [Theory]
    [MemberData(nameof(EventedAggregateNames))]
    public void PublicMutators_RaiseAnEvent(string aggregateName)
    {
        // Arrange
        var analysis = Analysis.Value;
        var aggregate = analysis.Definition(EventedAggregates.Single(t => t.FullName == aggregateName));

        // Act
        var unevented = UneventedPublicMutators(analysis, aggregate)
            .Where(m => !UneventedMutators.ContainsKey(m))
            .ToList();

        // Assert
        Listed(unevented).Should().BeEmpty(
            $"{aggregate.Name} records its history as events, so a public method that changes its state " +
            "without raising one leaves that history incomplete. Raise an event for the change (see " +
            "docs/contributing/domain-events.mdx), or, where the change is deliberately not history, add it " +
            "to UneventedMutators with the reason");
    }

    [Fact]
    public void UneventedMutators_AreStillUnevented()
    {
        // Arrange
        var analysis = Analysis.Value;

        // Act
        var flagged = EventedAggregates
            .Select(analysis.Definition)
            .SelectMany(a => UneventedPublicMutators(analysis, a))
            .ToHashSet();

        // Assert
        Listed(UneventedMutators.Keys.Except(flagged)).Should().BeEmpty(
            "an entry in UneventedMutators must name a public mutator that still changes state without " +
            "raising — remove any whose method is gone or now raises");
    }

    [Fact]
    public void SetBasedWrites_DoNotTargetAnEventedAggregate()
    {
        // Arrange
        var solutionRoot = AssemblyHelper.GetSolutionRoot();
        var eventedTables = EventedTables();

        // Act
        var offenders = new List<string>();
        var unresolved = new List<string>();
        var exemptionsUsed = new HashSet<string>();

        foreach (var file in SetBasedWriteSources())
        {
            var source = File.ReadAllText(file);
            var location = Path.GetRelativePath(solutionRoot, file);
            var writes = new List<string>();

            foreach (Match call in EfSetBasedWrite().Matches(source))
            {
                var target = EfTargetOf(source, call.Index);
                var line = LineOf(source, call.Index);

                if (target is null)
                    unresolved.Add($"{location}:{line}");
                else if (EventedAggregates.Any(a => a.IsAssignableFrom(target)))
                    writes.Add($"{location}:{line} {call.Groups["verb"].Value} on {target.Name}");
            }

            foreach (Match statement in SqlWrite().Matches(source))
            {
                var table = SqlTargetOf(statement, eventedTables);
                if (table is not null)
                    writes.Add($"{location}:{LineOf(source, statement.Index)} {statement.Groups["verb"].Value} on {table}");
            }

            if (writes.Count > 0 && IsMigration(file) && SetBasedWriteMigrations.ContainsKey(Path.GetFileName(file)))
                exemptionsUsed.Add(Path.GetFileName(file));
            else
                offenders.AddRange(writes);
        }

        // Assert
        Listed(unresolved).Should().BeEmpty(
            "the set-based write's target could not be read from its statement, so it cannot be checked. Start " +
            "the query from a named DbSet on the context (dbContext.Projects...)");
        Listed(offenders).Should().BeEmpty(
            "a set-based write changes rows without loading the aggregate, so no event is raised and the " +
            "aggregate's history silently misses the change. Load the records and change them through the " +
            "aggregate's methods, or, for a migration that accounts for the change another way, add it to " +
            "SetBasedWriteMigrations with the reason");
        Listed(SetBasedWriteMigrations.Keys.Except(exemptionsUsed)).Should().BeEmpty(
            "an entry in SetBasedWriteMigrations must name a migration that still writes an evented table");
    }

    /// <summary>
    /// Every public method on the aggregate, or on a domain base class it inherits from, that can change
    /// state and cannot raise. Factories count; property getters and the common entity plumbing do not.
    /// </summary>
    private static IEnumerable<string> UneventedPublicMutators(DomainMethodAnalysis analysis, TypeDefinition aggregate)
    {
        for (var type = aggregate; type is not null && !DomainMethodAnalysis.IsEntityPlumbing(type); type = type.BaseType?.Resolve())
        {
            foreach (var method in type.Methods)
            {
                if (!method.IsPublic || method.IsGetter || method.IsAbstract || method.Name.Contains('<'))
                    continue;
                if (method.Name is "Equals" or "GetHashCode" or "ToString")
                    continue;
                // A static method matters only as a factory; anything else static holds no instance's state.
                if (method.IsStatic && !method.ReturnType.FullName.Contains(aggregate.FullName, StringComparison.Ordinal))
                    continue;

                if (analysis.ChangesState(method) && !analysis.Raises(method))
                    yield return Describe(method);
            }
        }
    }

    /// <summary>
    /// One line per item, asserted as a string so the failure names every offender rather than the first.
    /// </summary>
    private static string Listed(IEnumerable<string?> items) => string.Join(Environment.NewLine, items);

    private static string Describe(MethodDefinition method)
    {
        var name = method.IsConstructor ? "ctor" : method.Name;
        var parameters = string.Join(", ", method.Parameters.Select(p => TypeLabel(p.ParameterType)));
        return $"{method.DeclaringType.Name}.{name}({parameters})";
    }

    // With type arguments, so overloads differing only in them get distinct allow-list keys.
    private static string TypeLabel(TypeReference type) => type switch
    {
        GenericInstanceType generic =>
            $"{generic.Name[..generic.Name.IndexOf('`')]}<{string.Join(", ", generic.GenericArguments.Select(TypeLabel))}>",
        ByReferenceType byReference => TypeLabel(byReference.ElementType) + "&",
        ArrayType array => TypeLabel(array.ElementType) + "[]",
        _ => type.Name,
    };

    /// <summary>The table each evented aggregate is stored in, as <c>schema.table</c> and bare name.</summary>
    private static Dictionary<string, string> EventedTables()
    {
        var tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in WaydModel.Model.GetEntityTypes())
        {
            if (!EventedAggregates.Any(a => a.IsAssignableFrom(entityType.ClrType)) || entityType.GetTableName() is not { } table)
                continue;

            var qualified = $"{entityType.GetSchema()}.{table}";
            tables[qualified] = qualified;
            tables[table] = qualified;
        }

        return tables;
    }

    /// <summary>
    /// Shipped source, and the migrations added after this guard. The ones before it have run everywhere and
    /// cannot be changed; several backfilled or renamed values on tables that are evented today.
    /// </summary>
    private static IEnumerable<string> SetBasedWriteSources()
    {
        return SourceFilesUnder(AssemblyHelper.GetSolutionRoot())
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(f => !IsMigration(f) || string.CompareOrdinal(Path.GetFileName(f), LastUnguardedMigration) > 0);
    }

    // Pruned while walking, so the client's node_modules and every build output are never entered.
    private static IEnumerable<string> SourceFilesUnder(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj" or "tests" or ".git" or ".claude" or "node_modules" or ".next"
                || name.EndsWith("Tests", StringComparison.Ordinal)
                || name.EndsWith("TestData", StringComparison.Ordinal))
                continue;

            foreach (var file in SourceFilesUnder(child))
                yield return file;
        }
    }

    /// <summary>The newest migration when this guard was added. Compared by file name, which leads with its id.</summary>
    private const string LastUnguardedMigration = "20260922023138_ActivityLogIdentityKey.cs";

    private static bool IsMigration(string path) => SegmentsOf(path).Contains("Migrations");

    private static string[] SegmentsOf(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The entity a set-based call writes, read from the DbSet its query starts from: the first DbSet named in
    /// the statement that ends in the call, outside any parentheses. A DbSet inside them is read by a lambda
    /// or a braceless <c>if</c>'s condition, not written; only a query wrapped whole in parentheses, with
    /// nothing outside, falls back to the first one inside.
    /// </summary>
    private static Type? EfTargetOf(string source, int callIndex)
    {
        var start = source.LastIndexOfAny([';', '{', '}'], callIndex) + 1;
        var statement = source[start..callIndex];

        var candidates = MemberAccess().Matches(statement)
            .Select(m => (Depth: ParenthesisDepth(statement, m.Index), Entity: EntityNamed(m)))
            .Where(c => c.Entity is not null)
            .ToList();

        return candidates.FirstOrDefault(c => c.Depth == 0).Entity ?? candidates.FirstOrDefault().Entity;
    }

    private static Type? EntityNamed(Match member)
    {
        if (member.Groups["set"].Success)
            return WaydModel.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == member.Groups["set"].Value)?.ClrType;

        return WaydModel.DbSetsByName.GetValueOrDefault(member.Groups["name"].Value);
    }

    private static int ParenthesisDepth(string text, int index)
    {
        var depth = 0;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
                depth--;
        }

        return depth;
    }

    /// <summary>
    /// The evented table a SQL write targets, or null. <c>UPDATE p SET … FROM … [Ppm].[Projects] p</c> writes
    /// through an alias, resolved to the table it is declared on in a <c>FROM</c> or <c>JOIN</c>; a table the
    /// statement only reads from is not its target.
    /// </summary>
    private static string? SqlTargetOf(Match statement, Dictionary<string, string> eventedTables)
    {
        var target = TableName(statement.Groups["target"].Value);
        if (eventedTables.TryGetValue(target, out var table))
            return table;

        var aliased = SqlAliasedTable().Matches(statement.Groups["rest"].Value)
            .FirstOrDefault(m => string.Equals(m.Groups["alias"].Value, target, StringComparison.OrdinalIgnoreCase));
        return aliased is not null && eventedTables.TryGetValue(TableName(aliased.Groups["table"].Value), out table) ? table : null;
    }

    private static string TableName(string reference) =>
        reference.Replace("[", "").Replace("]", "").Replace("\"", "");

    private static int LineOf(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    [GeneratedRegex(@"\.(?<verb>ExecuteUpdate|ExecuteDelete)(Async)?\s*\(")]
    private static partial Regex EfSetBasedWrite();

    [GeneratedRegex(@"\bSet<(?<set>\w+)>\s*\(|\.\s*(?<name>\w+)\b")]
    private static partial Regex MemberAccess();

    // Uppercase only, as SQL in this codebase is written: a C# Update( or Delete( never matches.
    [GeneratedRegex(@"\b(?<verb>UPDATE|DELETE(\s+FROM)?|INSERT(\s+INTO)?|MERGE(\s+INTO)?)\s+(TOP\s*\(\d+\)\s*)?(?<target>[\[""]?\w+[\]""]?(\.[\[""]?\w+[\]""]?)?)(?<rest>[^;]{0,2000})")]
    private static partial Regex SqlWrite();

    [GeneratedRegex(@"\b(FROM|JOIN)\s+(?<table>[\[""]?\w+[\]""]?(\.[\[""]?\w+[\]""]?)?)\s+(AS\s+)?(?<alias>\w+)")]
    private static partial Regex SqlAliasedTable();
}
