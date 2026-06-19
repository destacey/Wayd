namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// Defines all known feature flag names used in the application.
/// Add new entries here when introducing a feature flag.
/// The seeder will automatically create any missing flags on startup.
/// </summary>
public static class FeatureFlags
{
    public static readonly FeatureFlagDefinition PlanningPoker = new(Names.PlanningPoker, "Planning Poker", "Controls visibility of the Planning Poker feature.");

    /// <summary>
    /// Compile-time constant names for use in attributes (e.g., [FeatureGate]).
    /// </summary>
    public static class Names
    {
        public const string PlanningPoker = "planning-poker";
    }
}

/// <summary>
/// Represents a feature flag definition to be seeded.
/// </summary>
public sealed record FeatureFlagDefinition(string Name, string DisplayName, string? Description);
