namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment's editable details taken together, as <see cref="EnvironmentDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record EnvironmentDetails(string Name, int RingOrder);
