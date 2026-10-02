using Wayd.Common.Domain.Interfaces.Planning.Iterations;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// The current state of one sprint, or null when it no longer exists. Read by the Planning module, which keeps
/// a copy of each sprint, when an event reaches it for a sprint it has no copy of.
/// </summary>
public sealed record GetSimpleIterationQuery(Guid Id) : IQuery<ISimpleIteration?>;
