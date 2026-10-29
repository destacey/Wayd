using Wayd.Common.Domain.Tagging;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTags;

/// <summary>
/// A free tag's new name and description. Every request carrying the tag reads the new name.
/// </summary>
public sealed record UpdateFreeIntakeRequestTagRequest
{
    /// <summary>
    /// The label, unique among free intake request tags, ignoring case.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the label means, or null for no description.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class UpdateFreeIntakeRequestTagRequestValidator : CustomValidator<UpdateFreeIntakeRequestTagRequest>
{
    public UpdateFreeIntakeRequestTagRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(TagBase.NameMaxLength);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
