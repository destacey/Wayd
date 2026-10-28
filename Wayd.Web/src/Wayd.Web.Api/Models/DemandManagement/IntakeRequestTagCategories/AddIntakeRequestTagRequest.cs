namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// A tag to add to a tag axis.
/// </summary>
public sealed record AddIntakeRequestTagRequest
{
    /// <summary>
    /// The label, unique within the axis.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the label means, where it is not obvious.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class AddIntakeRequestTagRequestValidator : CustomValidator<AddIntakeRequestTagRequest>
{
    public AddIntakeRequestTagRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
