using Wayd.Common.Application.SystemSettings.ProductTags.Commands;

namespace Wayd.Web.Api.Models.Admin.SystemSettings;

public sealed record UpdateProductTagSettingsRequest
{
    /// <summary>
    /// Whether anyone who can tag a product may also give it free tags — tags on no category, typed in while
    /// tagging. Products already carrying free tags keep them when this is turned off.
    /// </summary>
    public bool AllowsFreeTags { get; set; }

    public UpdateProductTagSettingsCommand ToUpdateProductTagSettingsCommand() => new(AllowsFreeTags);
}
