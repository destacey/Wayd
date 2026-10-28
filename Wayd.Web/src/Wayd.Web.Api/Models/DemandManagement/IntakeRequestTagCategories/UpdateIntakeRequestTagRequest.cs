namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// A tag's new name and description. Every request carrying the tag reads the new name.
/// </summary>
public sealed record UpdateIntakeRequestTagRequest
{
    /// <summary>
    /// The label, unique within the axis.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the label means, or null for no description.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class UpdateIntakeRequestTagRequestValidator : CustomValidator<UpdateIntakeRequestTagRequest>
{
    public UpdateIntakeRequestTagRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
