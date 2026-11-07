namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// Every feature flag the application knows, and the only source of each flag's display name,
/// description and module. Add an entry here when introducing a flag: on startup the seeder creates
/// missing flags, brings existing ones in line with these definitions, and archives flags no longer
/// listed here.
/// </summary>
public static class FeatureFlags
{
    /// <summary>Following records, the Following list and in-app notifications.</summary>
    public static readonly FeatureFlagDefinition Following = new(Names.Following, "Following", "Follow projects, programs, PI objectives and intake requests, with a Following list and in-app notifications when they change.", FeatureFlagModule.Platform);
    /// <summary>The hosted MCP endpoint.</summary>
    public static readonly FeatureFlagDefinition McpServer = new(Names.McpServer, "MCP Server", "The hosted MCP endpoint (/mcp) that exposes the Wayd API as tools for AI agents.", FeatureFlagModule.Platform);
    /// <summary>Planning Poker sessions.</summary>
    public static readonly FeatureFlagDefinition PlanningPoker = new(Names.PlanningPoker, "Planning Poker", "Estimate work together in live poker sessions.", FeatureFlagModule.Planning);
    /// <summary>Story maps.</summary>
    public static readonly FeatureFlagDefinition StoryMaps = new(Names.StoryMaps, "Story Maps", "Lay out work along a user journey to plan releases.", FeatureFlagModule.Planning);
    /// <summary>Monte Carlo delivery forecasts.</summary>
    public static readonly FeatureFlagDefinition DeliveryForecasting = new(Names.DeliveryForecasting, "Delivery Forecasting", "Monte Carlo forecasts of when work will finish, from team throughput.", FeatureFlagModule.Planning);
    /// <summary>Employee allocation to teams and team capacity planning.</summary>
    public static readonly FeatureFlagDefinition TeamCapacityManagement = new(Names.TeamCapacityManagement, "Team Capacity Management", "Allocate employees to teams and plan team capacity, including time away and working days.", FeatureFlagModule.Organization);
    /// <summary>The Product Management module; its switch.</summary>
    public static readonly FeatureFlagDefinition ProductManagement = new(Names.ProductManagement, "Product Management", "The Product Management module: product catalog, versions, releases and deployments.", FeatureFlagModule.ProductManagement, IsModuleSwitch: true);
    /// <summary>Incident severity scales and records; needs <see cref="ProductManagement"/> too.</summary>
    public static readonly FeatureFlagDefinition IncidentManagement = new(Names.IncidentManagement, "Incident Management", "Severity scales and incident records for products.", FeatureFlagModule.ProductManagement);
    /// <summary>The Demand Management module; its switch.</summary>
    public static readonly FeatureFlagDefinition DemandManagement = new(Names.DemandManagement, "Demand Management", "The Demand Management module: intake requests and the desks that review them.", FeatureFlagModule.DemandManagement, IsModuleSwitch: true);
    /// <summary>
    /// Work items' synced workflows, record types and schemes in Status Workflows settings. The sync keeps
    /// them whether the flag is on or off; it only decides whether administration shows them.
    /// </summary>
    public static readonly FeatureFlagDefinition WorkItemStatusWorkflows = new(Names.WorkItemStatusWorkflows, "Work Item Status Workflows", "Shows the workflows, record types and schemes synced for work items in Status Workflows settings. Work items do not follow them yet.", FeatureFlagModule.Platform);

    private static readonly Lazy<IReadOnlyList<FeatureFlagDefinition>> _all = new(() => typeof(FeatureFlags)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.FieldType == typeof(FeatureFlagDefinition))
        .Select(f => (FeatureFlagDefinition)f.GetValue(null)!)
        .ToArray());

    /// <summary>Every definition above, in declaration order.</summary>
    public static IReadOnlyList<FeatureFlagDefinition> All => _all.Value;

    /// <summary>
    /// Compile-time constant names for use in attributes (e.g., [FeatureGate]).
    /// </summary>
    public static class Names
    {
        public const string PlanningPoker = "planning-poker";
        public const string StoryMaps = "story-maps";
        public const string ProductManagement = "product-management";
        public const string DeliveryForecasting = "delivery-forecasting";
        public const string McpServer = "mcp-server";
        public const string TeamCapacityManagement = "team-capacity-management";
        public const string DemandManagement = "demand-management";
        public const string Following = "following";
        public const string IncidentManagement = "incident-management";
        public const string WorkItemStatusWorkflows = "work-item-status-workflows";
    }
}

/// <summary>
/// A feature flag as the code defines it.
/// </summary>
/// <param name="Name">The kebab-case key the code gates on. Never rename it: a renamed key is a new flag.</param>
/// <param name="DisplayName">The name shown on the Feature Flags page.</param>
/// <param name="Description">What turning the flag on makes available, shown beneath its name.</param>
/// <param name="Module">The area of the app the flag belongs to.</param>
/// <param name="IsModuleSwitch">
/// Whether the flag turns its whole module on or off. The page shows it in the module's header and
/// disables the module's other flags while it is off. At most one flag per module.
/// </param>
/// <param name="DefaultEnabled">
/// The flag's state when the seeder first creates it. Admins toggle it afterwards; the seeder never
/// changes an existing flag's state.
/// </param>
public sealed record FeatureFlagDefinition(
    string Name,
    string DisplayName,
    string Description,
    FeatureFlagModule Module,
    bool IsModuleSwitch = false,
    bool DefaultEnabled = false);
