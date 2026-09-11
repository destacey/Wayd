namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio's editable details taken together, as <see cref="ProjectPortfolioDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ProjectPortfolioDetails(string Name, string Description);
