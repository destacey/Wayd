using Wayd.Common.Domain.Interfaces.Planning.Iterations;

namespace Wayd.Common.Application.Requests.Planning.Iterations;

/// <summary>
/// The current state of one Planning iteration, or null when it no longer exists. Read by the Work module,
/// which keeps a copy of each iteration, when an event reaches it for an iteration it has no copy of.
/// </summary>
public sealed record GetSimpleIterationQuery(Guid Id) : IQuery<ISimpleIteration?>;
