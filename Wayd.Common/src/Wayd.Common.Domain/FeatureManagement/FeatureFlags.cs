namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// Defines all known feature flag names used in the application.
/// Add new entries here when introducing a feature flag.
/// The seeder will automatically create any missing flags on startup.
/// </summary>
public static class FeatureFlags
{
    public static readonly FeatureFlagDefinition PlanningPoker = new(Names.PlanningPoker, "Planning Poker", "Controls visibility of the Planning Poker feature.");
    public static readonly FeatureFlagDefinition StoryMaps = new(Names.StoryMaps, "Story Maps", "Controls visibility of the Story Maps feature.");
    public static readonly FeatureFlagDefinition ProductManagement = new(Names.ProductManagement, "Product Management", "Controls availability of the Product Management module.");
    public static readonly FeatureFlagDefinition DeliveryForecasting = new(Names.DeliveryForecasting, "Delivery Forecasting", "Controls availability of Monte Carlo delivery forecasts.");
    public static readonly FeatureFlagDefinition McpServer = new(Names.McpServer, "MCP Server", "Controls availability of the hosted MCP endpoint (/mcp) that exposes the API as agent tools.");
    public static readonly FeatureFlagDefinition TeamCapacityManagement = new(Names.TeamCapacityManagement, "Team Capacity Management", "Controls availability of employee allocation to teams and team capacity planning.");
    public static readonly FeatureFlagDefinition DemandManagement = new(Names.DemandManagement, "Demand Management", "Controls availability of the Demand Management module: intake requests and the desks that review them.");
    public static readonly FeatureFlagDefinition Following = new(Names.Following, "Following", "Controls availability of following projects, programs, planning interval objectives and intake requests, the Following list, and in-app notifications about changes to those records.");
    public static readonly FeatureFlagDefinition IncidentManagement = new(Names.IncidentManagement, "Incident Management", "Controls availability of incident management under product management, including severity scales and incident records.");
    public static readonly FeatureFlagDefinition WysiwygMarkdownEditor = new(Names.WysiwygMarkdownEditor, "WYSIWYG Markdown Editor", "Replaces the markdown text editor on long-text fields with a document editor that formats as you type. Stored text stays markdown.");

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
        public const string WysiwygMarkdownEditor = "wysiwyg-markdown-editor";
    }
}

/// <summary>
/// Represents a feature flag definition to be seeded.
/// <paramref name="DefaultEnabled"/> sets the initial state when the seeder first
/// creates the flag (admins can toggle it afterward; the seeder never overwrites
/// an existing flag).
/// </summary>
public sealed record FeatureFlagDefinition(string Name, string DisplayName, string? Description, bool DefaultEnabled = false);
