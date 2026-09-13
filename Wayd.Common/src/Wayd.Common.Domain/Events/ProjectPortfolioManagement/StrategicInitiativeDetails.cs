namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative's editable details taken together, as
/// <see cref="StrategicInitiativeDetailsUpdatedEvent.Previous"/> records the values an edit replaced.
/// </summary>
public sealed record StrategicInitiativeDetails(string Name, string Description);
