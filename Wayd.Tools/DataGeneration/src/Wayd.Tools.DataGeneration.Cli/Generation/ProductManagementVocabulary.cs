namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The names the Product Management generator draws on. Product types, statuses, tags and environment
/// categories are the values Wayd seeds or defines, so they are matched exactly here — an import resolves
/// each by name and refuses what it cannot find.
/// </summary>
internal static class ProductManagementVocabulary
{
    // ---- Reference data Wayd seeds -----------------------------------------------------------

    internal const string ProductLineType = "Product Line";
    internal const string ProductType = "Product";
    internal const string ServiceType = "Service";
    internal const string ApplicationType = "Application";
    internal const string LibraryType = "Library";
    internal const string ToolType = "Tool";

    internal const string ConceptStatus = "Concept";
    internal const string ActiveStatus = "Active";
    internal const string SunsetStatus = "Sunset";
    internal const string RetiredStatus = "Retired";

    /// <summary>The seeded tag category, written the way the import reads a tag: <c>Category|Tag</c>.</summary>
    private const string PlatformCategory = "Platform";

    internal static string PlatformTag(string tag) => $"{PlatformCategory}|{tag}";

    internal const string Development = "Development";
    internal const string Testing = "Testing";
    internal const string Staging = "Staging";
    internal const string Production = "Production";

    internal const string Succeeded = "Succeeded";
    internal const string Failed = "Failed";
    internal const string RolledBack = "RolledBack";

    internal const string Changed = "Changed";
    internal const string CarriedForward = "CarriedForward";

    // ---- Naming --------------------------------------------------------------------------------

    /// <summary>What a product line is called after its domain — "Payments Cloud".</summary>
    internal static readonly string[] ProductLineSuffixes = ["Suite", "Cloud"];

    /// <summary>The name a single-ART product takes when the ART adds nothing to its domain's name.</summary>
    internal static readonly string[] ProductNouns = ["Hub", "Studio", "Console", "Manager", "Portal"];

    internal static readonly string[] ServiceNouns = ["Service", "API", "Engine", "Gateway"];
    internal static readonly string[] WebApplicationNouns = ["Web", "Portal", "Dashboard"];
    internal static readonly string[] LibraryNouns = ["SDK", "Client"];
    internal static readonly string[] ToolNouns = ["CLI", "Toolkit"];

    /// <summary>Components still at the idea stage, hung off an existing team's work.</summary>
    internal static readonly string[] ConceptNouns = ["Assistant", "Insights", "Automation", "Forecasting"];

    // ---- Notes ---------------------------------------------------------------------------------

    internal static readonly string[] EngineeringNotes =
    [
        "Dependency upgrades and minor fixes.",
        "Performance improvements to the hot paths.",
        "Bug fixes and logging improvements.",
        "New endpoints behind a feature flag.",
        "Reduced cold-start time and memory use.",
        "Accessibility fixes across the main flows.",
        "Retry handling for upstream timeouts.",
        "Schema migration and backfill.",
        "Security patches for third-party packages.",
        "Observability: new metrics and trace spans.",
    ];

    internal static readonly string[] HotfixNotes =
    [
        "Hotfix for a regression in the previous version.",
        "Reverts a faulty configuration change.",
        "Fixes an error spike seen after release.",
        "Patches a data-handling defect found in production.",
    ];

    internal static readonly string[] FailureReasons =
    [
        "Health checks failed after rollout.",
        "Database migration timed out.",
        "Smoke tests failed.",
        "Configuration value missing in the target environment.",
        "Container image failed to pull.",
        "Error rate exceeded the rollout threshold.",
    ];

    internal static readonly string[] RollbackReasons =
    [
        "Error rate rose after release; rolled back.",
        "Latency regression detected by monitoring.",
        "Customer-reported defect in a core flow.",
        "Memory leak under production load.",
        "Incompatible change with a downstream consumer.",
    ];

    internal static readonly string[] ReleaseHighlights =
    [
        "faster load times", "improved reporting", "new integrations", "accessibility improvements",
        "a refreshed navigation", "bulk editing", "better search", "expanded audit history",
        "new notification preferences", "reliability improvements",
    ];
}
