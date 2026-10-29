namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide settings for how products are tagged.
/// </summary>
public sealed record ProductTagSettings : ISettingsSection<ProductTagSettings>
{
    public static string Key => "productTags";

    /// <summary>
    /// Whether anyone who can tag a product may also give it free tags — tags on no axis, typed in while tagging.
    /// Off until an admin turns it on, so product filing stays curated unless the organization chooses otherwise.
    /// Products already carrying free tags keep them when it is turned off.
    /// </summary>
    public bool AllowsFreeTags { get; init; }
}
