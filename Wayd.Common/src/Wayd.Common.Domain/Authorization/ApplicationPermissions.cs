using System.Collections.ObjectModel;

namespace Wayd.Common.Domain.Authorization;

public static class ApplicationAction
{
    // Common
    public const string View = nameof(View);
    public const string Search = nameof(Search);
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);
    public const string Manage = nameof(Manage);
    public const string Import = nameof(Import);
    public const string Export = nameof(Export);
    public const string Generate = nameof(Generate);
    public const string Clean = nameof(Clean);
    public const string Run = nameof(Run);

    // Specific
    public const string ManageTeamMemberships = nameof(ManageTeamMemberships);
    public const string ManageProjectWorkItems = nameof(ManageProjectWorkItems);
}

public static class ApplicationResource
{
    public const string Hangfire = nameof(Hangfire);
    public const string BackgroundJobs = nameof(BackgroundJobs);

    public const string Users = nameof(Users);
    public const string UserRoles = nameof(UserRoles);
    public const string Roles = nameof(Roles);
    public const string RoleClaims = nameof(RoleClaims);
    public const string Permissions = nameof(Permissions);
    public const string OidcProviders = nameof(OidcProviders);

    public const string ScoringModels = nameof(ScoringModels);

    public const string Connections = nameof(Connections);
    public const string Connectors = nameof(Connectors);

    public const string Employees = nameof(Employees);
    public const string Teams = nameof(Teams);
    public const string TeamMemberRoles = nameof(TeamMemberRoles);

    public const string PlanningIntervals = nameof(PlanningIntervals);
    public const string PlanningIntervalObjectives = nameof(PlanningIntervalObjectives);
    public const string Risks = nameof(Risks);
    public const string Roadmaps = nameof(Roadmaps);
    public const string Iterations = nameof(Iterations);
    public const string PokerSessions = nameof(PokerSessions);
    public const string EstimationScales = nameof(EstimationScales);

    public const string ExpenditureCategories = nameof(ExpenditureCategories);
    public const string ProjectPortfolios = nameof(ProjectPortfolios);
    public const string Projects = nameof(Projects);
    public const string Programs = nameof(Programs);
    public const string PpmStrategicThemes = nameof(PpmStrategicThemes);
    public const string StrategicInitiatives = nameof(StrategicInitiatives);
    public const string ProjectLifecycles = nameof(ProjectLifecycles);

    public const string StrategicThemes = nameof(StrategicThemes);
    public const string Strategies = nameof(Strategies);
    public const string Visions = nameof(Visions);

    public const string WorkTypeTiers = nameof(WorkTypeTiers);
    public const string WorkTypeLevels = nameof(WorkTypeLevels);
    public const string Workspaces = nameof(Workspaces);
    public const string WorkItems = nameof(WorkItems);
    public const string WorkProcesses = nameof(WorkProcesses);
    public const string WorkStatusCategories = nameof(WorkStatusCategories);
    public const string WorkStatuses = nameof(WorkStatuses);
    public const string WorkTypes = nameof(WorkTypes);

    public const string Links = nameof(Links);

    public const string HealthChecks = nameof(HealthChecks);

    public const string FeatureFlags = nameof(FeatureFlags);
}

public static class ApplicationPermissions
{
    private static readonly ApplicationPermission[] _common = [];

    private const string ApplicationCategory = "Application";
    private static readonly ApplicationPermission[] _application =
    [
        new ("View Scoring Models", ApplicationAction.View, ApplicationResource.ScoringModels, ApplicationCategory),
        new ("Create Scoring Models", ApplicationAction.Create, ApplicationResource.ScoringModels, ApplicationCategory),
        new ("Update Scoring Models", ApplicationAction.Update, ApplicationResource.ScoringModels, ApplicationCategory),
        new ("Delete Scoring Models", ApplicationAction.Delete, ApplicationResource.ScoringModels, ApplicationCategory),

        new("View Feature Flags", ApplicationAction.View, ApplicationResource.FeatureFlags, ApplicationCategory),
        new("Create Feature Flags", ApplicationAction.Create, ApplicationResource.FeatureFlags, ApplicationCategory),
        new("Update Feature Flags", ApplicationAction.Update, ApplicationResource.FeatureFlags, ApplicationCategory),
        new("Delete Feature Flags", ApplicationAction.Delete, ApplicationResource.FeatureFlags, ApplicationCategory),
    ];

    private const string BackgroundJobsCategory = "Background Jobs";
    private static readonly ApplicationPermission[] _backgroundJobs =
    [
        new("View Hangfire", ApplicationAction.View, ApplicationResource.Hangfire, BackgroundJobsCategory),
        new("View Background Jobs", ApplicationAction.View, ApplicationResource.BackgroundJobs, BackgroundJobsCategory),
        new("Create Background Jobs", ApplicationAction.Create, ApplicationResource.BackgroundJobs, BackgroundJobsCategory),
        new("Run Background Jobs", ApplicationAction.Run, ApplicationResource.BackgroundJobs, BackgroundJobsCategory)
    ];

    private const string IdentityCategory = "Identity";
    private static readonly ApplicationPermission[] _identity =
    [
        new("View Users", ApplicationAction.View, ApplicationResource.Users, IdentityCategory),
        new("Search Users", ApplicationAction.Search, ApplicationResource.Users, IdentityCategory),
        new("Create Users", ApplicationAction.Create, ApplicationResource.Users, IdentityCategory),
        new("Update Users", ApplicationAction.Update, ApplicationResource.Users, IdentityCategory),
        new("Delete Users", ApplicationAction.Delete, ApplicationResource.Users, IdentityCategory),
        new("Export Users", ApplicationAction.Export, ApplicationResource.Users, IdentityCategory),

        new("View UserRoles", ApplicationAction.View, ApplicationResource.UserRoles, IdentityCategory),
        new("Update UserRoles", ApplicationAction.Update, ApplicationResource.UserRoles, IdentityCategory),

        new("View Roles", ApplicationAction.View, ApplicationResource.Roles, IdentityCategory),
        new("Create Roles", ApplicationAction.Create, ApplicationResource.Roles, IdentityCategory),
        new("Update Roles", ApplicationAction.Update, ApplicationResource.Roles, IdentityCategory),
        new("Delete Roles", ApplicationAction.Delete, ApplicationResource.Roles, IdentityCategory),

        new("View RoleClaims", ApplicationAction.View, ApplicationResource.RoleClaims, IdentityCategory),
        new("Update RoleClaims", ApplicationAction.Update, ApplicationResource.RoleClaims, IdentityCategory),

        new("View Permissions", ApplicationAction.View, ApplicationResource.Permissions, IdentityCategory),

        new("View OIDC Providers", ApplicationAction.View, ApplicationResource.OidcProviders, IdentityCategory),
        new("Create OIDC Providers", ApplicationAction.Create, ApplicationResource.OidcProviders, IdentityCategory),
        new("Update OIDC Providers", ApplicationAction.Update, ApplicationResource.OidcProviders, IdentityCategory),
        new("Delete OIDC Providers", ApplicationAction.Delete, ApplicationResource.OidcProviders, IdentityCategory)
    ];

    private const string IntegrationsCategory = "Integrations";
    private static readonly ApplicationPermission[] _appIntegration =
    [
        new("View Connections", ApplicationAction.View, ApplicationResource.Connections, IntegrationsCategory),
        new("Create Connections", ApplicationAction.Create, ApplicationResource.Connections, IntegrationsCategory),
        new("Update Connections", ApplicationAction.Update, ApplicationResource.Connections, IntegrationsCategory),
        new("Delete Connections", ApplicationAction.Delete, ApplicationResource.Connections, IntegrationsCategory),

        new("View Connectors", ApplicationAction.View, ApplicationResource.Connectors, IntegrationsCategory),
    ];

    private const string HealthCategory = "Health";
    private static readonly ApplicationPermission[] _healthChecks =
    [
        new("View Health Checks", ApplicationAction.View, ApplicationResource.HealthChecks, HealthCategory),
        new("Create Health Checks", ApplicationAction.Create, ApplicationResource.HealthChecks, HealthCategory),
        new("Update Health Checks", ApplicationAction.Update, ApplicationResource.HealthChecks, HealthCategory),
    ];

    private const string LinksCategory = "Links";
    private static readonly ApplicationPermission[] _links =
    [
        new("View Links", ApplicationAction.View, ApplicationResource.Links, LinksCategory),
        new("Create Links", ApplicationAction.Create, ApplicationResource.Links, LinksCategory),
        new("Update Links", ApplicationAction.Update, ApplicationResource.Links, LinksCategory),
        new("Delete Links", ApplicationAction.Delete, ApplicationResource.Links, LinksCategory),
    ];

    private const string OrganizationCategory = "Organization";
    private static readonly ApplicationPermission[] _organization =
    [
        new("View Employees", ApplicationAction.View, ApplicationResource.Employees, OrganizationCategory),
        new("Create Employees", ApplicationAction.Create, ApplicationResource.Employees, OrganizationCategory),
        new("Update Employees", ApplicationAction.Update, ApplicationResource.Employees, OrganizationCategory),
        new("Delete Employees", ApplicationAction.Delete, ApplicationResource.Employees, OrganizationCategory),

        new("View Teams and Teams of Teams", ApplicationAction.View, ApplicationResource.Teams, OrganizationCategory),
        new("Create Teams", ApplicationAction.Create, ApplicationResource.Teams, OrganizationCategory),
        new("Update Teams", ApplicationAction.Update, ApplicationResource.Teams, OrganizationCategory),
        new("Manage Team Memberships.  This includes adding, updating, and removing team memberships.", ApplicationAction.ManageTeamMemberships, ApplicationResource.Teams, OrganizationCategory),
        new("Delete Teams", ApplicationAction.Delete, ApplicationResource.Teams, OrganizationCategory),

        new("View Team Member Roles", ApplicationAction.View, ApplicationResource.TeamMemberRoles, OrganizationCategory),
        new("Create Team Member Roles", ApplicationAction.Create, ApplicationResource.TeamMemberRoles, OrganizationCategory),
        new("Update Team Member Roles", ApplicationAction.Update, ApplicationResource.TeamMemberRoles, OrganizationCategory),
        new("Delete Team Member Roles", ApplicationAction.Delete, ApplicationResource.TeamMemberRoles, OrganizationCategory),
    ];

    private const string PlanningCategory = "Planning";
    private static readonly ApplicationPermission[] _planning =
    [
        new("View Planning Intervals", ApplicationAction.View, ApplicationResource.PlanningIntervals, PlanningCategory),
        new("Create Planning Intervals", ApplicationAction.Create, ApplicationResource.PlanningIntervals, PlanningCategory),
        new("Update Planning Intervals", ApplicationAction.Update, ApplicationResource.PlanningIntervals, PlanningCategory),
        new("Delete Planning Intervals", ApplicationAction.Delete, ApplicationResource.PlanningIntervals, PlanningCategory),

        new("View Planning Interval Objectives", ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives, PlanningCategory),
        new("Create, update, and delete Planning Interval Objectives", ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives, PlanningCategory),
        new("Import Planning Interval Objectives", ApplicationAction.Import, ApplicationResource.PlanningIntervalObjectives, PlanningCategory),

        new("View Risks", ApplicationAction.View, ApplicationResource.Risks, PlanningCategory),
        new("Create Risks", ApplicationAction.Create, ApplicationResource.Risks, PlanningCategory),
        new("Update Risks", ApplicationAction.Update, ApplicationResource.Risks, PlanningCategory),
        new("Delete Risks", ApplicationAction.Delete, ApplicationResource.Risks, PlanningCategory),
        new("Import Risks", ApplicationAction.Import, ApplicationResource.Risks, PlanningCategory),

        new("View Roadmaps", ApplicationAction.View, ApplicationResource.Roadmaps, PlanningCategory),
        new("Create Roadmaps", ApplicationAction.Create, ApplicationResource.Roadmaps, PlanningCategory),
        new("Update Roadmaps", ApplicationAction.Update, ApplicationResource.Roadmaps, PlanningCategory),
        new("Delete Roadmaps", ApplicationAction.Delete, ApplicationResource.Roadmaps, PlanningCategory),

        new("View Iterations", ApplicationAction.View, ApplicationResource.Iterations, PlanningCategory),

        new("View Poker Sessions", ApplicationAction.View, ApplicationResource.PokerSessions, PlanningCategory),
        new("Create Poker Sessions", ApplicationAction.Create, ApplicationResource.PokerSessions, PlanningCategory),
        new("Update Poker Sessions", ApplicationAction.Update, ApplicationResource.PokerSessions, PlanningCategory),
        new("Delete Poker Sessions", ApplicationAction.Delete, ApplicationResource.PokerSessions, PlanningCategory),

        new("View Estimation Scales", ApplicationAction.View, ApplicationResource.EstimationScales, PlanningCategory),
        new("Create Estimation Scales", ApplicationAction.Create, ApplicationResource.EstimationScales, PlanningCategory),
        new("Update Estimation Scales", ApplicationAction.Update, ApplicationResource.EstimationScales, PlanningCategory),
        new("Delete Estimation Scales", ApplicationAction.Delete, ApplicationResource.EstimationScales, PlanningCategory),
    ];

    private const string PpmCategory = "Project Portfolio Management";
    private static readonly ApplicationPermission[] _projectPortfolioManagement =
    [
        new ("View Expenditure Categories", ApplicationAction.View, ApplicationResource.ExpenditureCategories, PpmCategory),
        new ("Create Expenditure Categories", ApplicationAction.Create, ApplicationResource.ExpenditureCategories, PpmCategory),
        new ("Update Expenditure Categories", ApplicationAction.Update, ApplicationResource.ExpenditureCategories, PpmCategory),
        new ("Delete Expenditure Categories", ApplicationAction.Delete, ApplicationResource.ExpenditureCategories, PpmCategory),

        new ("View PPM Strategic Themes", ApplicationAction.View, ApplicationResource.PpmStrategicThemes, PpmCategory),

        new ("View Portfolios", ApplicationAction.View, ApplicationResource.ProjectPortfolios, PpmCategory),
        new ("Create Portfolios", ApplicationAction.Create, ApplicationResource.ProjectPortfolios, PpmCategory),
        new ("Update Portfolios", ApplicationAction.Update, ApplicationResource.ProjectPortfolios, PpmCategory),
        new ("Delete Portfolios", ApplicationAction.Delete, ApplicationResource.ProjectPortfolios, PpmCategory),

        new ("View Programs", ApplicationAction.View, ApplicationResource.Programs, PpmCategory),
        new ("Create Programs", ApplicationAction.Create, ApplicationResource.Programs, PpmCategory),
        new ("Update Programs", ApplicationAction.Update, ApplicationResource.Programs, PpmCategory),
        new ("Delete Programs", ApplicationAction.Delete, ApplicationResource.Programs, PpmCategory),

        new ("View Projects", ApplicationAction.View, ApplicationResource.Projects, PpmCategory),
        new ("Create Projects", ApplicationAction.Create, ApplicationResource.Projects, PpmCategory),
        new ("Update Projects", ApplicationAction.Update, ApplicationResource.Projects, PpmCategory),
        new ("Delete Projects", ApplicationAction.Delete, ApplicationResource.Projects, PpmCategory),
        new ("Manage Project Work Items", ApplicationAction.ManageProjectWorkItems, ApplicationResource.Projects, PpmCategory),

        new ("View Strategic Initiatives", ApplicationAction.View, ApplicationResource.StrategicInitiatives, PpmCategory),
        new ("Create Strategic Initiatives", ApplicationAction.Create, ApplicationResource.StrategicInitiatives, PpmCategory),
        new ("Update Strategic Initiatives", ApplicationAction.Update, ApplicationResource.StrategicInitiatives, PpmCategory),
        new ("Delete Strategic Initiatives", ApplicationAction.Delete, ApplicationResource.StrategicInitiatives, PpmCategory),

        new ("View Project Lifecycles", ApplicationAction.View, ApplicationResource.ProjectLifecycles, PpmCategory),
        new ("Create Project Lifecycles", ApplicationAction.Create, ApplicationResource.ProjectLifecycles, PpmCategory),
        new ("Update Project Lifecycles", ApplicationAction.Update, ApplicationResource.ProjectLifecycles, PpmCategory),
        new ("Delete Project Lifecycles", ApplicationAction.Delete, ApplicationResource.ProjectLifecycles, PpmCategory),
    ];

    private const string StrategicManagementCategory = "Strategic Management";
    private static readonly ApplicationPermission[] _strategicManagement =
    [
        new ("View Strategic Themes", ApplicationAction.View, ApplicationResource.StrategicThemes, StrategicManagementCategory),
        new ("Create Strategic Themes", ApplicationAction.Create, ApplicationResource.StrategicThemes, StrategicManagementCategory),
        new ("Update Strategic Themes", ApplicationAction.Update, ApplicationResource.StrategicThemes, StrategicManagementCategory),
        new ("Delete Strategic Themes", ApplicationAction.Delete, ApplicationResource.StrategicThemes, StrategicManagementCategory),

        new ("View Strategies", ApplicationAction.View, ApplicationResource.Strategies, StrategicManagementCategory),
        new ("Create Strategies", ApplicationAction.Create, ApplicationResource.Strategies, StrategicManagementCategory),
        new ("Update Strategies", ApplicationAction.Update, ApplicationResource.Strategies, StrategicManagementCategory),
        new ("Delete Strategies", ApplicationAction.Delete, ApplicationResource.Strategies, StrategicManagementCategory),

        new ("View Visions", ApplicationAction.View, ApplicationResource.Visions, StrategicManagementCategory),
        new ("Create Visions", ApplicationAction.Create, ApplicationResource.Visions, StrategicManagementCategory),
        new ("Update Visions", ApplicationAction.Update, ApplicationResource.Visions, StrategicManagementCategory),
        new ("Delete Visions", ApplicationAction.Delete, ApplicationResource.Visions, StrategicManagementCategory),
    ];

    private const string WorkManagementCategory = "Work Management";
    private static readonly ApplicationPermission[] _work =
    [
        new("View work type tiers", ApplicationAction.View, ApplicationResource.WorkTypeTiers, WorkManagementCategory),

        new("View work type levels", ApplicationAction.View, ApplicationResource.WorkTypeLevels, WorkManagementCategory),
        new("Create work type levels", ApplicationAction.Create, ApplicationResource.WorkTypeLevels, WorkManagementCategory),
        new("Update work type levels", ApplicationAction.Update, ApplicationResource.WorkTypeLevels, WorkManagementCategory),
        new("Delete work type levels", ApplicationAction.Delete, ApplicationResource.WorkTypeLevels, WorkManagementCategory),

        new("View Workspaces", ApplicationAction.View, ApplicationResource.Workspaces, WorkManagementCategory),
        new("Create Workspaces", ApplicationAction.Create, ApplicationResource.Workspaces, WorkManagementCategory),
        new("Update Workspaces", ApplicationAction.Update, ApplicationResource.Workspaces, WorkManagementCategory),
        new("Delete Workspaces", ApplicationAction.Delete, ApplicationResource.Workspaces, WorkManagementCategory),

        new("View WorkItems", ApplicationAction.View, ApplicationResource.WorkItems, WorkManagementCategory),
        new("Create WorkItems", ApplicationAction.Create, ApplicationResource.WorkItems, WorkManagementCategory),
        new("Update WorkItems", ApplicationAction.Update, ApplicationResource.WorkItems, WorkManagementCategory),
        new("Delete WorkItems", ApplicationAction.Delete, ApplicationResource.WorkItems, WorkManagementCategory),

        new("View WorkTypes", ApplicationAction.View, ApplicationResource.WorkProcesses, WorkManagementCategory),
        new("Create WorkTypes", ApplicationAction.Create, ApplicationResource.WorkProcesses, WorkManagementCategory),
        new("Update WorkTypes", ApplicationAction.Update, ApplicationResource.WorkProcesses, WorkManagementCategory),
        new("Delete WorkTypes", ApplicationAction.Delete, ApplicationResource.WorkProcesses, WorkManagementCategory),

        new("View WorkStatusCategories", ApplicationAction.View, ApplicationResource.WorkStatusCategories, WorkManagementCategory),

        new("View WorkStatuses", ApplicationAction.View, ApplicationResource.WorkStatuses, WorkManagementCategory),
        new("Create WorkStatuses", ApplicationAction.Create, ApplicationResource.WorkStatuses, WorkManagementCategory),
        new("Update WorkStatuses", ApplicationAction.Update, ApplicationResource.WorkStatuses, WorkManagementCategory),
        new("Delete WorkStatuses", ApplicationAction.Delete, ApplicationResource.WorkStatuses, WorkManagementCategory),

        new("View WorkTypes", ApplicationAction.View, ApplicationResource.WorkTypes, WorkManagementCategory),
        new("Create WorkTypes", ApplicationAction.Create, ApplicationResource.WorkTypes, WorkManagementCategory),
        new("Update WorkTypes", ApplicationAction.Update, ApplicationResource.WorkTypes, WorkManagementCategory),
        new("Delete WorkTypes", ApplicationAction.Delete, ApplicationResource.WorkTypes, WorkManagementCategory),
    ];

    private static readonly ApplicationPermission[] _all = [.. _common
        .Union(_application)
        .Union(_backgroundJobs)
        .Union(_identity)
        .Union(_appIntegration)
        .Union(_healthChecks)
        .Union(_links)
        .Union(_organization)
        .Union(_planning)
        .Union(_projectPortfolioManagement)
        .Union(_strategicManagement)
        .Union(_work)];

    public static IReadOnlyList<ApplicationPermission> All { get; } = new ReadOnlyCollection<ApplicationPermission>(_all);
    public static IReadOnlyList<ApplicationPermission> Root { get; } = new ReadOnlyCollection<ApplicationPermission>([.. _all.Where(p => p.IsRoot)]);
    public static IReadOnlyList<ApplicationPermission> Admin { get; } = new ReadOnlyCollection<ApplicationPermission>([.. _all.Where(p => !p.IsRoot)]);
}

public record ApplicationPermission(string Description, string Action, string Resource, string Category = "", bool IsRoot = false)
{
    public string Name => NameFor(Action, Resource);
    public static string NameFor(string action, string resource) => $"Permissions.{resource}.{action}";
}