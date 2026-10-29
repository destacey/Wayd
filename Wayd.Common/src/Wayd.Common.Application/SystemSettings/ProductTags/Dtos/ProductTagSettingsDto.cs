namespace Wayd.Common.Application.SystemSettings.ProductTags.Dtos;

public sealed record ProductTagSettingsDto
{
    /// <summary>
    /// Whether anyone who can tag a product may also give it free tags — tags on no axis, typed in while tagging.
    /// </summary>
    public bool AllowsFreeTags { get; init; }
}
