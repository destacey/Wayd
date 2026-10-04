using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// The state of each iteration now, keyed by id. State follows each sprint's actual and default dates in its
/// team's zone and is never stored, so a module keeping a copy of a sprint reads it here. An id that matches no
/// iteration is missing from the result.
/// </summary>
public sealed record GetIterationStatesQuery(IReadOnlyCollection<Guid> Ids) : IQuery<IReadOnlyDictionary<Guid, IterationState>>;
