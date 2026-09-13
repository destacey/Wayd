using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// An iteration's descriptive details taken together, as <see cref="IterationDetailsUpdatedEvent.Previous"/>
/// records the values a change replaced. The Work copy's details event uses the same shape.
/// </summary>
public sealed record IterationDetails(string Name, IterationType Type);
