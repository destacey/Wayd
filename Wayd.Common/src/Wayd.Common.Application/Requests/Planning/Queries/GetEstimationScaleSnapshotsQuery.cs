using Wayd.Common.Domain.Models.Planning.EstimationScales;

namespace Wayd.Common.Application.Requests.Planning.Queries;

/// <summary>
/// The current state of Planning's estimation scales, with every version: the one scale named by
/// <paramref name="Id"/>, or every scale when it is null. Read by a module keeping a copy of the scales when an
/// event reaches it for a scale it has no copy of, and to repair the copies.
/// </summary>
/// <remarks>
/// A scale that no longer exists is absent, so asking for one that was deleted answers an empty list.
/// </remarks>
public sealed record GetEstimationScaleSnapshotsQuery(Guid? Id = null) : IQuery<IReadOnlyList<EstimationScaleSnapshot>>;
