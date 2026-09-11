namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's editable details taken together, as <see cref="ProjectDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ProjectDetails(
    string Name,
    string Description,
    int ExpenditureCategoryId,
    string? BusinessCase,
    string? ExpectedBenefits);
