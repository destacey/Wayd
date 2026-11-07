using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.FeatureManagement;

/// <summary>
/// The stored state of a feature flag defined in <see cref="FeatureFlags"/>. Its name, description and
/// module mirror the code's definition, which the seeder reapplies on startup; only whether it is
/// enabled belongs to the organization.
/// </summary>
public sealed class FeatureFlag : BaseAuditableEntity<int>
{
    private FeatureFlag() { }

    private FeatureFlag(string name, string displayName, string? description, FeatureFlagModule module, bool isModuleSwitch, bool isEnabled)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
        Module = module;
        IsModuleSwitch = isModuleSwitch;
        IsEnabled = isEnabled;
        IsArchived = false;
    }

    /// <summary>The longest display name the database stores.</summary>
    public const int DisplayNameMaxLength = 128;

    /// <summary>The longest description the database stores.</summary>
    public const int DescriptionMaxLength = 1024;

    /// <summary>
    /// The programmatic key used in code (e.g., "new-dashboard-layout").
    /// </summary>
    public string Name { get; private init; } = default!;

    /// <summary>
    /// The name shown on the Feature Flags page, copied from the code's definition.
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// What turning the flag on makes available, copied from the code's definition.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The area of the app the flag belongs to, copied from the code's definition.
    /// </summary>
    public FeatureFlagModule Module { get; private set; }

    /// <summary>
    /// Whether the flag turns its whole module on or off, copied from the code's definition.
    /// </summary>
    public bool IsModuleSwitch { get; private set; }

    /// <summary>
    /// Whether this feature is globally enabled.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Whether the flag is no longer defined in code. An archived flag is always disabled.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// JSON-serialized filter configuration for future targeting support.
    /// </summary>
    public string? FiltersJson { get; private set; }

    /// <summary>
    /// Turns the flag on or off.
    /// </summary>
    public void Toggle(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Brings the flag in line with its definition in code, restoring it if it was archived. Leaves
    /// <see cref="IsEnabled"/> alone, since that is the organization's choice.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public bool SyncWith(FeatureFlagDefinition definition)
    {
        if (!string.Equals(definition.Name.Trim(), Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Definition '{definition.Name}' does not describe flag '{Name}'.", nameof(definition));

        var displayName = definition.DisplayName.Trim();
        var description = definition.Description.Trim();

        var changed = DisplayName != displayName
            || Description != description
            || Module != definition.Module
            || IsModuleSwitch != definition.IsModuleSwitch
            || IsArchived;

        DisplayName = displayName;
        Description = description;
        Module = definition.Module;
        IsModuleSwitch = definition.IsModuleSwitch;
        IsArchived = false;

        return changed;
    }

    /// <summary>
    /// Archives a flag that is no longer defined in code, turning it off.
    /// </summary>
    public Result Archive()
    {
        if (IsArchived)
            return Result.Failure("Feature flag is already archived.");

        IsArchived = true;
        IsEnabled = false;
        return Result.Success();
    }

    /// <summary>
    /// Creates the stored flag for a definition, in the definition's default state.
    /// </summary>
    public static Result<FeatureFlag> Create(FeatureFlagDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            return Result.Failure<FeatureFlag>("Name is required.");

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
            return Result.Failure<FeatureFlag>("Display name is required.");

        var trimmedName = definition.Name.Trim().ToLowerInvariant();
        if (!IsValidName(trimmedName))
            return Result.Failure<FeatureFlag>("Name must be kebab-case (lowercase letters, numbers, and hyphens only).");

        return Result.Success(new FeatureFlag(
            trimmedName,
            definition.DisplayName.Trim(),
            definition.Description.Trim(),
            definition.Module,
            definition.IsModuleSwitch,
            definition.DefaultEnabled));
    }

    private static bool IsValidName(string name) =>
        Regex.IsMatch(name, @"^[a-z0-9]+(-[a-z0-9]+)*$");
}
