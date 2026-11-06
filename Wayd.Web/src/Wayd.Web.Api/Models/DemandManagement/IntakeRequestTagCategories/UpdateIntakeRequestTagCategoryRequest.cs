using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// A tag category's new name and description.
/// </summary>
public sealed record UpdateIntakeRequestTagCategoryRequest
{
    /// <summary>
    /// The category being changed; must match the route.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What the company calls the category. Unique among active and inactive categories.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the category is for, or null for no description.
    /// </summary>
    public string? Description { get; set; }

    public UpdateIntakeRequestTagCategoryCommand ToUpdateIntakeRequestTagCategoryCommand() => new(Id, Name, Description);
}

public sealed class UpdateIntakeRequestTagCategoryRequestValidator : CustomValidator<UpdateIntakeRequestTagCategoryRequest>
{
    public UpdateIntakeRequestTagCategoryRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
