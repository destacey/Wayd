using Wayd.ProductManagement.Application.ProductTags.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTags;

/// <summary>
/// Merges a free tag into another tag.
/// </summary>
public sealed record MergeProductTagRequest
{
    /// <summary>
    /// The tag the free tag's products move to: another free tag, or an active tag on an active category.
    /// </summary>
    public Guid TargetTagId { get; set; }

    public MergeProductTagCommand ToMergeProductTagCommand(Guid id) => new(id, TargetTagId);
}

public sealed class MergeProductTagRequestValidator : CustomValidator<MergeProductTagRequest>
{
    public MergeProductTagRequestValidator()
    {
        RuleFor(r => r.TargetTagId)
            .NotEmpty();
    }
}
