using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mono.Cecil;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Links.Models;
using Wayd.Organization.Domain.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Work.Domain.Models;
using PpmStrategicTheme = Wayd.ProjectPortfolioManagement.Domain.Models.StrategicTheme;
using StrategicTheme = Wayd.StrategicManagement.Domain.Models.StrategicTheme;
using Strategy = Wayd.StrategicManagement.Domain.Models.Strategy;
using Version = Wayd.ProductManagement.Domain.Models.Version;
using Vision = Wayd.StrategicManagement.Domain.Models.Vision;

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
        // App Integration
        typeof(AzureDevOpsBoardsConnection),
        typeof(AzureOpenAIConnection),
        typeof(EntraConnection),
        typeof(WorkdayConnection),

        // Common
        typeof(Employee),
        typeof(ExternalIdentityMapping),
        typeof(OidcProvider),
        typeof(PersonalAccessToken),
        typeof(ScoringModel),
        typeof(StatusWorkflow),
        typeof(SystemSettingsSection),
        typeof(WorkflowAssignment),

        // Organization
        typeof(Team),
        typeof(TeamOfTeams),

        // Planning
        typeof(PlanningInterval),
        typeof(PlanningIntervalObjective),
        typeof(Risk),
        typeof(Roadmap),

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
        typeof(Iteration),
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
        ["ProjectPortfolio.MoveProjectRanks(PpmActor, IReadOnlyList<Guid>, Nullable<Guid>, Nullable<Guid>)"] =
            "Rank is display order on the portfolio's board, positioned between neighbours; not a fact about any project.",
        ["ProjectPortfolio.RebalanceRanks(PpmActor)"] =
            "Renumbers every project's rank without changing their order.",
        ["WorkdayConnection.RecordInitResult(Boolean, IReadOnlyList<String>, IReadOnlyList<String>, String, IReadOnlyList<WorkdayOrgType>, DateTimeOffset)"] =
            "Records the result of probing the configuration, not a change to it.",
        ["HealthCheckBase.Update(HealthStatus, Instant, String, Instant)"] =
            "In Common, so it cannot be internal to the aggregates that own a health check; only their UpdateHealthCheck calls it, and that raises.",
        ["HealthCheckBase.ChangeExpiration(Instant)"] =
            "Called only by HealthReport when a new check supersedes the latest, inside the owning aggregate's AddHealthCheck, which raises.",

        // Not yet evented: each awaits the issue it names.
        ["Project.CreateTask(Int32, String, String, ProjectTaskType, TaskStatus, TaskPriority, Progress, Guid, FlexibleDateRange, Nullable<LocalDate>, Nullable<Decimal>, Dictionary<TaskRole, HashSet<Guid>>)"] = "#950.",
        ["Project.ChangeTaskPlacement(Guid, Guid, Nullable<Int32>)"] = "#950.",
        ["Project.DeleteTask(Guid)"] = "#950.",
        ["Project.LinkTaskParents()"] = "#950.",
        ["Project.UpdateTaskDates(Guid, FlexibleDateRange, Nullable<LocalDate>, Boolean)"] = "#950.",
        ["Project.UpdateStageDates(Guid, FlexibleDateRange)"] = "#950.",
        ["Project.RecalculateAncestorsForTask(ProjectTask)"] = "#950.",
        ["ProjectTask.UpdateDetails(String, String, TaskPriority)"] = "#950.",
        ["ProjectTask.UpdateStatus(TaskStatus, Instant)"] = "#950.",
        ["ProjectTask.UpdateProgress(Progress)"] = "#950.",
        ["ProjectTask.UpdatePlannedDates(FlexibleDateRange, Nullable<LocalDate>)"] = "#950.",
        ["ProjectTask.UpdateEffort(Nullable<Decimal>)"] = "#950.",
        ["ProjectTask.AddDependency(ProjectTask)"] = "#950.",
        ["ProjectTask.RemoveDependency(Guid, Instant)"] = "#950.",
        ["ProjectStage.UpdateDescription(String)"] = "#950.",
        ["ProjectStage.UpdateStatus(TaskStatus)"] = "#950.",
        ["ProjectStage.UpdatePlannedDates(FlexibleDateRange)"] = "#950.",
        ["ProjectStage.UpdateProgress(Progress)"] = "#950.",
        ["WorkProcess.CreateExternal(String, String, Guid, Instant)"] = "#955.",
        ["WorkProcess.Update(String, String, Instant)"] = "#955.",
        ["WorkProcess.AddWorkType(Int32, Guid, Boolean, Instant)"] = "#955.",
        ["WorkProcess.ActivateWorkType(Int32, Instant)"] = "#955.",
        ["WorkProcess.DeactivateWorkType(Int32, Instant)"] = "#955.",
        ["WorkProcess.ChangeWorkTypeWorkflow(Int32, Guid, Instant)"] = "#955.",
        ["AzureDevOpsBoardsConnection.SetSystemId(String)"] = "#956.",
        ["AzureDevOpsBoardsConnection.SyncWorkspaces(IEnumerable<AzureDevOpsBoardsWorkspace>, Instant)"] = "#956.",
        ["AzureDevOpsBoardsConnection.SyncProcesses(IEnumerable<AzureDevOpsBoardsWorkProcess>, Instant)"] = "#956.",
        ["AzureDevOpsBoardsConnection.SyncTeams(List<IExternalTeam>, Instant)"] = "#956.",
        ["AzureDevOpsBoardsConnection.UpdateWorkProcessIntegrationState(IntegrationRegistration<Guid, Guid>, Instant)"] = "#956.",
        ["AzureDevOpsBoardsConnection.ClearWorkProcessIntegrationState(Guid, Instant)"] = "#956.",
        ["AzureDevOpsBoardsConnection.UpdateWorkspaceIntegrationState(IntegrationRegistration<Guid, Guid>, Instant)"] = "#956.",
    };

    /// <summary>
    /// The aggregates that raise no events, each with the reason: either it is deliberately not history, or
    /// it awaits the issue that will event it. Every type a DbSet exposes is on this list,
    /// <see cref="EventedAggregates"/> or <see cref="ChildEntities"/> — see
    /// <see cref="EveryEntity_IsEventedUneventedOrAChild"/>.
    /// </summary>
    /// <remarks>
    /// Synced data is not a reason on its own: the sync is when Wayd learns the fact, and an aggregate that
    /// raises only on a real change raises in proportion to change, not to how often it syncs.
    /// </remarks>
    private static readonly Dictionary<Type, string> UneventedAggregates = new()
    {
        [typeof(WorkItem)] =
            "Synced at thousands of genuine changes per run, which would dominate the log; the work system keeps field-level revisions.",
        [typeof(PokerSession)] =
            "A short-lived collaborative session with a change per vote; live updates go out over SignalR.",
        [typeof(StoryMap)] =
            "A drafting canvas of fine-grained edits whose current state is the artifact.",

        // Copies built from another module's events, which are their history.
        [typeof(PlanningSprint)] = "A copy of Work's Iteration.",
        [typeof(PlanningTeam)] = "A copy of Organization's team.",
        [typeof(PpmStrategicTheme)] = "A copy of Strategic Management's StrategicTheme.",
        [typeof(PpmTeam)] = "A copy of Organization's team.",
        [typeof(WorkProject)] = "A copy of PPM's Project.",
        [typeof(WorkTeam)] = "A copy of Organization's team.",
        [typeof(User)] = "A read-only view over the Identity user, whose ApplicationUser raises the events.",

        // Records that are themselves a log or the state of a run.
        [typeof(ActivityLogEntry)] = "The log the events are recorded in.",
        [typeof(StatusTransition)] = "A transition row the owning record writes alongside its status-changed event.",
        [typeof(ImportProcess)] = "The state of an import run, advanced per chunk; it is the record of the run.",
        [typeof(SyncRun)] = "The state of a sync run; it is the record of the run.",
        [typeof(WorkItemStateHistory)] = "A work item's history imported from the source's revisions; it is the record.",

        // Derived or side-effect data with no meaning of its own.
        [typeof(ExternalEmployeeBlacklistItem)] =
            "External ids a removed employee must not be re-imported from; written as part of that removal.",
        [typeof(WorkflowAliasName)] = "Lookup data rebuilt from code at startup.",

        // Not yet evented: each awaits the issue it names.
        [typeof(Strategy)] = "#951.",
        [typeof(Vision)] = "#951.",
        [typeof(EstimationScale)] = "#952.",
        [typeof(ExpenditureCategory)] = "#952.",
        [typeof(FeatureFlag)] = "#952.",
        [typeof(Link)] = "#952.",
        [typeof(ProductTagCategory)] = "#952.",
        [typeof(ProductType)] = "#952.",
        [typeof(ProjectLifecycle)] = "#952.",
        [typeof(TeamMemberRole)] = "#952.",
        [typeof(WorkItemReference)] = "#952.",
        [typeof(Workflow)] = "#955.",
        [typeof(WorkStatus)] = "#955.",
        [typeof(WorkType)] = "#955.",
        [typeof(WorkTypeHierarchy)] = "#955.",
        [typeof(Workspace)] = "#955.",
    };

    /// <summary>
    /// Entities with a DbSet of their own that belong to another aggregate, by the aggregate they belong to.
    /// A child of an evented aggregate is part of its history, so its own public mutators must raise too.
    /// </summary>
    private static readonly Dictionary<Type, Type> ChildEntities = new()
    {
        [typeof(ImportProcessRow)] = typeof(ImportProcess),
        [typeof(PlanningIntervalIterationSprint)] = typeof(PlanningInterval),
        [typeof(PlanningIntervalObjectiveHealthCheck)] = typeof(PlanningIntervalObjective),
        [typeof(ProductDependency)] = typeof(Product),
        [typeof(ProductTag)] = typeof(ProductTagCategory),
        [typeof(ProductTagAssignment)] = typeof(Product),
        [typeof(ProjectHealthCheck)] = typeof(Project),
        [typeof(ProjectScore)] = typeof(Project),
        [typeof(ProjectStage)] = typeof(Project),
        [typeof(ProjectStatusHistory)] = typeof(Project),
        [typeof(ProjectTask)] = typeof(Project),
        [typeof(ProjectTaskDependency)] = typeof(Project),
        [typeof(ReleasePackageComponent)] = typeof(ReleasePackage),
        [typeof(ReleasePackageInclusion)] = typeof(Release),
        [typeof(ReleaseVersion)] = typeof(Release),
        [typeof(TeamMember)] = typeof(BaseTeam),
        [typeof(TeamOperatingModel)] = typeof(Team),
        [typeof(WorkflowStatus)] = typeof(StatusWorkflow),
        [typeof(WorkItemDependency)] = typeof(WorkItem),
        [typeof(WorkItemHierarchy)] = typeof(WorkItem),
    };

    /// <summary>
    /// Migrations that write an evented aggregate's table on purpose, by file name, each with the reason —
    /// typically one that writes the matching activity log entries itself, as the baseline backfills do.
    /// </summary>
    private static readonly Dictionary<string, string> SetBasedWriteMigrations = new()
    {
        ["20260926204406_Store-ReleasePackage-ReleasedAt-As-Instant.cs"] =
            "Retypes ReleasedDate to the ReleasedAt instant, carrying each value across; no fact about a package changed: #929.",
        ["20260926212850_Store-Version-Cut-And-Released-As-Instants.cs"] =
            "Retypes CutDate and ReleasedDate to the CutAt and ReleasedAt instants, carrying each value across; no fact about a version changed: #929.",
    };

    /// <summary>
    /// Shipped source that writes an evented aggregate's table set-based on purpose, by file name, each with
    /// the reason. Only for a column that is not part of the aggregate's history at all.
    /// </summary>
    private static readonly Dictionary<string, string> SetBasedWriteSourcesOutsideHistory = new()
    {
        ["LastSeenWriter.cs"] =
            "Stamps PersonalAccessToken.LastUsedAt on authentication, batched and conditional; usage telemetry, not a change to the token.",
    };

    private static readonly Lazy<DomainMethodAnalysis> Analysis = new(DomainMethodAnalysis.Load);

    public static TheoryData<string> HistoryTypeNames() => new(HistoryTypes().Select(t => t.FullName!).Order());

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

    [Fact]
    public void EveryEntity_IsEventedUneventedOrAChild()
    {
        // Arrange
        var entities = WaydModel.DbSetsByName.Values.Where(IsWaydDomainType).Distinct().ToList();

        // Act
        var placements = entities
            .Select(e => (Entity: e, Lists: (IsEvented(e) ? 1 : 0) + (UneventedAggregates.ContainsKey(e) ? 1 : 0) + (ChildEntities.ContainsKey(e) ? 1 : 0)))
            .ToList();

        // Assert
        Listed(placements.Where(p => p.Lists == 0).Select(p => p.Entity.FullName)).Should().BeEmpty(
            "every entity is either an evented aggregate, an unevented one with the reason, or a child of " +
            "another aggregate. Add it to EventedAggregates by raising its events, to UneventedAggregates with " +
            "why its changes are not history (or the issue that will event it), or to ChildEntities");
        Listed(placements.Where(p => p.Lists > 1).Select(p => p.Entity.FullName)).Should().BeEmpty(
            "an entity is on more than one of EventedAggregates, UneventedAggregates and ChildEntities. An " +
            "aggregate that now raises comes off UneventedAggregates");
        Listed(UneventedAggregates.Keys.Concat(ChildEntities.Keys).Except(entities).Select(t => t.FullName)).Should().BeEmpty(
            "an entry in UneventedAggregates or ChildEntities must name an entity the context still exposes");
    }

    [Theory]
    [MemberData(nameof(HistoryTypeNames))]
    public void PublicMutators_RaiseAnEvent(string typeName)
    {
        // Arrange
        var analysis = Analysis.Value;
        var type = analysis.Definition(HistoryTypes().Single(t => t.FullName == typeName));

        // Act
        var unevented = UneventedPublicMutators(analysis, type)
            .Where(m => !UneventedMutators.ContainsKey(m))
            .ToList();

        // Assert
        Listed(unevented).Should().BeEmpty(
            $"{type.Name} is part of an evented aggregate's history, so a public method that changes its " +
            "state without raising an event leaves that history incomplete. Raise an event for the change " +
            "(see docs/contributing/domain-events.mdx), make a child's method internal so only its aggregate " +
            "changes it, or, where the change is deliberately not history, add it to UneventedMutators with " +
            "the reason");
    }

    [Fact]
    public void UneventedMutators_AreStillUnevented()
    {
        // Arrange
        var analysis = Analysis.Value;

        // Act
        var flagged = HistoryTypes()
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

            var exemptions = IsMigration(file) ? SetBasedWriteMigrations : SetBasedWriteSourcesOutsideHistory;
            if (writes.Count > 0 && exemptions.ContainsKey(Path.GetFileName(file)))
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
            "SetBasedWriteMigrations with the reason. A column that is not history at all goes on " +
            "SetBasedWriteSourcesOutsideHistory");
        Listed(SetBasedWriteMigrations.Keys.Concat(SetBasedWriteSourcesOutsideHistory.Keys).Except(exemptionsUsed)).Should().BeEmpty(
            "an entry in SetBasedWriteMigrations or SetBasedWriteSourcesOutsideHistory must name a file that " +
            "still writes an evented table");
    }

    /// <summary>The evented aggregates and the children that belong to one: every type whose changes are history.</summary>
    private static IEnumerable<Type> HistoryTypes() =>
        EventedAggregates.Concat(ChildEntities.Where(c => IsEvented(c.Value)).Select(c => c.Key));

    /// <summary>An evented aggregate, or an abstract base whose concrete aggregates are.</summary>
    private static bool IsEvented(Type type) => EventedAggregates.Any(type.IsAssignableFrom);

    /// <summary>
    /// Wayd's own entities outside Infrastructure. The Identity entities there (<c>ApplicationUser</c> and
    /// <c>ApplicationRole</c>, which raise) are outside the domain assemblies this guard reads.
    /// </summary>
    private static bool IsWaydDomainType(Type type) =>
        type.Assembly.GetName().Name!.StartsWith("Wayd.", StringComparison.Ordinal)
        && type.Assembly != typeof(WaydDbContext).Assembly;

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
