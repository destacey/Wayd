using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Goals;
using Wayd.Goals.Application.Persistence;
using Wayd.Infrastructure.Common.Services;
using Wayd.Links;
using Wayd.Links.Models;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.Common.Domain.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.StrategicManagement.Application;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models;
using PpmStrategicTheme = Wayd.ProjectPortfolioManagement.Domain.Models.StrategicTheme;
using StrategicTheme = Wayd.StrategicManagement.Domain.Models.StrategicTheme;

namespace Wayd.Infrastructure.Persistence.Context;

public class WaydDbContext : BaseDbContext, IAppIntegrationDbContext, IFeatureManagementDbContext, IGoalsDbContext, ILinksDbContext, IOrganizationDbContext, IPlanningDbContext, IProjectPortfolioManagementDbContext, IStrategicManagementDbContext, IWorkDbContext
{
    private static readonly ConcurrentDictionary<string, bool> _ftsAvailabilityCache = new();

    public WaydDbContext(DbContextOptions options, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, IOptions<DatabaseSettings> dbSettings, IEventPublisher events, IRequestCorrelationIdProvider requestCorrelationIdProvider)
        : base(options, currentUser, dateTimeProvider, dbSettings, events, requestCorrelationIdProvider)
    {
    }

    #region Common
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => Set<ExternalEmployeeBlacklistItem>();
    public DbSet<OidcProvider> OidcProviders => Set<OidcProvider>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();
    public DbSet<User> WaydUsers => Set<User>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();

    #endregion Common

    #region IFeatureManagement

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    #endregion IFeatureManagement

    #region IAppIntegration

    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<AzureDevOpsBoardsConnection> AzureDevOpsBoardsConnections => Set<AzureDevOpsBoardsConnection>();
    public DbSet<AzureOpenAIConnection> AzureOpenAIConnections => Set<AzureOpenAIConnection>();
    public DbSet<EntraConnection> EntraConnections => Set<EntraConnection>();
    public DbSet<WorkdayConnection> WorkdayConnections => Set<WorkdayConnection>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    #endregion IAppIntegration

    #region IGoals

    public DbSet<Objective> Objectives => Set<Objective>();

    #endregion IGoals

    #region ILinks

    public DbSet<Link> Links => Set<Link>();

    #endregion ILinks

    #region IOrganization

    public DbSet<BaseTeam> BaseTeams => Set<BaseTeam>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamOfTeams> TeamOfTeams => Set<TeamOfTeams>();
    public DbSet<TeamOperatingModel> TeamOperatingModels => Set<TeamOperatingModel>();
    public DbSet<TeamMemberRole> TeamMemberRoles => Set<TeamMemberRole>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    #endregion IOrganization

    #region IPlanning

    public DbSet<Iteration> Iterations => Set<Iteration>();
    public DbSet<PlanningIntervalObjective> PlanningIntervalObjectives => Set<PlanningIntervalObjective>();
    public DbSet<PlanningInterval> PlanningIntervals => Set<PlanningInterval>();
    public DbSet<PlanningIntervalIterationSprint> PlanningIntervalIterationSprints => Set<PlanningIntervalIterationSprint>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<PlanningTeam> PlanningTeams => Set<PlanningTeam>();
    public DbSet<PlanningIntervalObjectiveHealthCheck> PlanningIntervalObjectiveHealthChecks => Set<PlanningIntervalObjectiveHealthCheck>();
    public DbSet<Roadmap> Roadmaps => Set<Roadmap>();
    public DbSet<EstimationScale> EstimationScales => Set<EstimationScale>();
    public DbSet<PokerSession> PokerSessions => Set<PokerSession>();

    #endregion IPlanning

    #region IProjectPortfolioManagementDbContext

    public DbSet<ExpenditureCategory> ExpenditureCategories => Set<ExpenditureCategory>();
    public DbSet<ProjectPortfolio> Portfolios => Set<ProjectPortfolio>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectHealthCheck> ProjectHealthChecks => Set<ProjectHealthCheck>();
    public DbSet<ProjectScore> ProjectScores => Set<ProjectScore>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectTaskDependency> ProjectTaskDependencies => Set<ProjectTaskDependency>();
    public DbSet<PpmTeam> PpmTeams => Set<PpmTeam>();
    public DbSet<PpmStrategicTheme> PpmStrategicThemes => Set<PpmStrategicTheme>();
    public DbSet<StrategicInitiative> StrategicInitiatives => Set<StrategicInitiative>();
    public DbSet<ProjectLifecycle> ProjectLifecycles => Set<ProjectLifecycle>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<ScoringModel> ScoringModels => Set<ScoringModel>();

    #endregion IProjectPortfolioManagementDbContext

    #region IStrategicManagement

    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategicTheme> StrategicThemes => Set<StrategicTheme>();
    public DbSet<Vision> Visions => Set<Vision>();

    #endregion IStrategicManagement

    #region IWork

    public DbSet<WorkTypeHierarchy> WorkTypeHierarchies => Set<WorkTypeHierarchy>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkItemReference> WorkItemReferences => Set<WorkItemReference>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<WorkItemHierarchy> WorkItemHierarchies => Set<WorkItemHierarchy>();
    public DbSet<WorkIteration> WorkIterations => Set<WorkIteration>();
    public DbSet<WorkProcess> WorkProcesses => Set<WorkProcess>();
    public DbSet<WorkProject> WorkProjects => Set<WorkProject>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkStatus> WorkStatuses => Set<WorkStatus>();
    public DbSet<WorkTeam> WorkTeams => Set<WorkTeam>();
    public DbSet<WorkType> WorkTypes => Set<WorkType>();

    public IQueryable<WorkItem> SearchWorkItems(string searchTerm, int top)
    {
        var query = IsFtsAvailable()
            ? SearchWorkItemsFts(searchTerm)
            : SearchWorkItemsLike(searchTerm);

        return query
            .OrderBy(e => EF.Property<string>(e, "KeyPrefix"))
            .ThenBy(e => EF.Property<int>(e, "KeyNumber"))
            .Take(top);
    }

    private IQueryable<WorkItem> SearchWorkItemsFts(string searchTerm)
    {
        var ftsTerm = $"\"{searchTerm}*\"";
        return WorkItems
            .Where(e => EF.Functions.Contains(e.Title, ftsTerm)
                || EF.Functions.Contains((string)(object)e.Key, ftsTerm)
                || (e.ParentId.HasValue && EF.Functions.Contains((string)(object)e.Parent!.Key, ftsTerm)));
    }

    private IQueryable<WorkItem> SearchWorkItemsLike(string searchTerm)
    {
        return WorkItems
            .Where(e => e.Title.Contains(searchTerm)
                || ((string)e.Key).Contains(searchTerm)
                || (e.ParentId.HasValue && ((string)e.Parent!.Key).Contains(searchTerm)));
    }

    #endregion IWork

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaNames.Work);
    }

    #region Graph Table Sync

    // TODO: Find a better way to sync graph tables

    public async Task<int> UpsertTeamNode(TeamNode teamNode, CancellationToken cancellationToken)
    {
        return await Database.ExecuteSqlInterpolatedAsync($@"
            MERGE INTO [Organization].[TeamNodes] AS target
            USING (SELECT {teamNode.Id} AS Id) AS source (Id)
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET
                    [Key] = {teamNode.Key},
                    [Name] = {teamNode.Name},
                    [Code] = {teamNode.Code},
                    [Type] = {teamNode.Type.ToString()},  -- ToString() is required
                    [IsActive] = {teamNode.IsActive},
                    [ActiveDate] = {teamNode.ActiveDate},
                    [InactiveDate] = {teamNode.InactiveDate}
            WHEN NOT MATCHED THEN
                INSERT (
                    [Id],
                    [Key],
                    [Name],
                    [Code],
                    [Type],
                    [IsActive],
                    [ActiveDate],
                    [InactiveDate]
                )
                VALUES (
                    {teamNode.Id},
                    {teamNode.Key},
                    {teamNode.Name},
                    {teamNode.Code},
                    {teamNode.Type.ToString()}, -- ToString() is required
                    {teamNode.IsActive},
                    {teamNode.ActiveDate},
                    {teamNode.InactiveDate}
                );
        ", cancellationToken);
    }

    public async Task<int> UpsertTeamMembershipEdge(TeamMembershipEdge membershipEdge, CancellationToken cancellationToken)
    {
        return await Database.ExecuteSqlInterpolatedAsync($@"
           MERGE INTO [Organization].[TeamMembershipEdges] AS target
           USING (SELECT {membershipEdge.Id} AS Id) AS source (Id)
           ON target.Id = source.Id
           WHEN MATCHED THEN
               UPDATE SET
                   [StartDate] = {membershipEdge.StartDate},
                   [EndDate] = {membershipEdge.EndDate}
           WHEN NOT MATCHED THEN
               INSERT (
                   [Id],
                   [StartDate],
                   [EndDate],
                   $from_id,
                   $to_id
               )
               VALUES (
                   {membershipEdge.Id},
                   {membershipEdge.StartDate},
                   {membershipEdge.EndDate},
                   (SELECT $node_id FROM [Organization].[TeamNodes] WHERE Id = {membershipEdge.FromNode.Id}),
                   (SELECT $node_id FROM [Organization].[TeamNodes] WHERE Id = {membershipEdge.ToNode.Id})
               );
        ", cancellationToken);
    }

    public async Task<int> DeleteTeamMembershipEdge(Guid id, CancellationToken cancellationToken)
    {
        return await Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM [Organization].[TeamMembershipEdges]
            WHERE Id = {id}
        ", cancellationToken);
    }

    #endregion Graph Table Sync

    private bool IsFtsAvailable()
    {
        if (!Database.IsSqlServer()) return false;
        var connectionString = Database.GetConnectionString() ?? string.Empty;
        return _ftsAvailabilityCache.GetOrAdd(connectionString, _ =>
            Database.SqlQuery<int>($"SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') AS Value").FirstOrDefault() == 1);
    }
}