using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>When the team recorded a sprint as started and completed; null where it recorded nothing.</summary>
public sealed record SprintActualDates(Instant? Started, Instant? Completed);
