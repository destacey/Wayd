using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.Common.Application.Requests.ProjectPortfolioManagement;

/// <summary>
/// The current state of one PPM project with where it stands in its lifecycle, or null when it no longer
/// exists. Read by Demand Management, which keeps a copy of each project, when an event reaches it for a
/// project it has no copy of.
/// </summary>
public sealed record GetSimpleProjectWithLifecycleQuery(Guid Id) : IQuery<ISimpleProjectWithLifecycle?>;
