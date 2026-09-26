namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Who a stored settings section applies to.
/// </summary>
/// <remarks>
/// Stored by name, so a member may be added but never renamed. Only <see cref="System"/> exists today; the
/// column is there so per-organization or per-tenant overrides can be added without reshaping the table.
/// </remarks>
public enum SettingsScope
{
    /// <summary>The whole installation.</summary>
    System = 1,
}
