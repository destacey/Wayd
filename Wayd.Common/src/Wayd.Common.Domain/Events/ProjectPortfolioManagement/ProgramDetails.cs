namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A program's editable details taken together, as <see cref="ProgramDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ProgramDetails(string Name, string Description);
