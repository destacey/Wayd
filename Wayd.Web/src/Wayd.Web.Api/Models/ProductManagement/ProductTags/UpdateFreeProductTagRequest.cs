using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.ProductTags.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTags;

/// <summary>
/// Renames a free tag or changes its description.
/// </summary>
public sealed record UpdateFreeProductTagRequest
{
    /// <summary>
    /// What the tag is called. Unique among free product tags, ignoring case.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the tag means. Cleared when omitted.
    /// </summary>
    public string? Description { get; set; }

    public UpdateFreeProductTagCommand ToUpdateFreeProductTagCommand(Guid id) => new(id, Name, Description);
}

public sealed class UpdateFreeProductTagRequestValidator : CustomValidator<UpdateFreeProductTagRequest>
{
    public UpdateFreeProductTagRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(TagBase.NameMaxLength);

        RuleFor(r => r.Description)
            .MaximumLength(512);
    }
}
