using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// A tag category to add to the end of the list, with no tags.
/// </summary>
public sealed record CreateIntakeRequestTagCategoryRequest
{
    /// <summary>
    /// What the company calls the category. Unique among active and inactive categories.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the category is for, shown when choosing tags.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether a request can carry several tags from the category. When false, tagging a request replaces the tag it
    /// held in the category. Cannot be changed afterwards.
    /// </summary>
    public bool AllowsMany { get; set; }

    public CreateIntakeRequestTagCategoryCommand ToCreateIntakeRequestTagCategoryCommand() => new(Name, Description, AllowsMany);
}

public sealed class CreateIntakeRequestTagCategoryRequestValidator : CustomValidator<CreateIntakeRequestTagCategoryRequest>
{
    public CreateIntakeRequestTagCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
