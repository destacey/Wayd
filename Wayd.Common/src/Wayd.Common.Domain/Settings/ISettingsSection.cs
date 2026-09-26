namespace Wayd.Common.Domain.Settings;

/// <summary>
/// A group of related system settings, stored together as one <see cref="SystemSettingsSection"/> row.
/// </summary>
/// <remarks>
/// <para>
/// A section is a record whose property initializers are its defaults. A missing row, or a property the
/// stored JSON does not carry, reads as that default, so adding a setting is adding a property with a valid
/// default and needs no migration.
/// </para>
/// <para>
/// <see cref="Key"/> and the property names are stored, so neither can be renamed once shipped: a rename
/// orphans the saved values and silently reverts them to the defaults. Properties must be value-comparable
/// (no collections), because a save that changes nothing is detected by record equality.
/// </para>
/// </remarks>
public interface ISettingsSection<TSelf> : IEquatable<TSelf>
    where TSelf : class, ISettingsSection<TSelf>, new()
{
    /// <summary>The section's stored key, e.g. "scheduling".</summary>
    static abstract string Key { get; }

    /// <summary>
    /// The version of the section's stored shape. Bump it only for a change a stored row cannot absorb by
    /// falling back to defaults, and transform the older rows when reading them.
    /// </summary>
    static virtual int SchemaVersion => 1;
}
