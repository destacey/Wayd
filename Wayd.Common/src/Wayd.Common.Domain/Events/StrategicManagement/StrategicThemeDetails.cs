namespace Wayd.Common.Domain.Events.StrategicManagement;

/// <summary>
/// A strategic theme's editable details taken together, as <see cref="StrategicThemeDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record StrategicThemeDetails(string Name, string Description);
