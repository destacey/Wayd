namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// Defines all known feature flag names used in the application.
/// Add new entries here when introducing a feature flag.
/// The seeder will automatically create any missing flags on startup.
/// </summary>
public static class FeatureFlags
{
    public static readonly FeatureFlagDefinition PlanningPoker = new(Names.PlanningPoker, "Planning Poker", "Controls visibility of the Planning Poker feature.");

    public static readonly FeatureFlagDefinition NewTimelineUi = new(Names.NewTimelineUi, "Use New Timeline UI", "Renders roadmaps (and later objectives/PPM) with the new in-house timeline component instead of the legacy vis-timeline.", DefaultEnabled: true);

    /// <summary>
    /// Compile-time constant names for use in attributes (e.g., [FeatureGate]).
    /// </summary>
    public static class Names
    {
        public const string PlanningPoker = "planning-poker";
        public const string NewTimelineUi = "new-timeline-ui";
    }
}

/// <summary>
/// Represents a feature flag definition to be seeded.
/// <paramref name="DefaultEnabled"/> sets the initial state when the seeder first
/// creates the flag (admins can toggle it afterward; the seeder never overwrites
/// an existing flag).
/// </summary>
public sealed record FeatureFlagDefinition(string Name, string DisplayName, string? Description, bool DefaultEnabled = false);
