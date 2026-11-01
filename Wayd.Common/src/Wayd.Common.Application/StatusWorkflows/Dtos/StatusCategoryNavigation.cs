using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// A status category as a <see cref="SimpleNavigationDto"/>: the category's id, named as its owner type
/// labels it.
/// </summary>
/// <remarks>
/// The name is for display only. A reader that needs the category back reads the id, since a labelled
/// name is no longer a <see cref="StatusCategory"/> value.
/// </remarks>
internal static class StatusCategoryNavigation
{
    /// <summary>
    /// Describes <paramref name="category"/> in <paramref name="owner"/>'s words, or by the category's
    /// own name when the owner type is not registered.
    /// </summary>
    public static SimpleNavigationDto For(StatusCategory category, WorkflowOwnerDescriptor? owner) => new()
    {
        Id = (int)category,
        Name = owner?.CategoryLabel(category) ?? category.ToString(),
    };
}
