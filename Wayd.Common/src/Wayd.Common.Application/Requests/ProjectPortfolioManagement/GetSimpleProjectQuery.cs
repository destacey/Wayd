using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.Common.Application.Requests.ProjectPortfolioManagement;

/// <summary>
/// The current state of one PPM project, or null when it no longer exists. Read by the Work module, which
/// keeps a copy of each project, when an event reaches it for a project it has no copy of.
/// </summary>
public sealed record GetSimpleProjectQuery(Guid Id) : IQuery<ISimpleProject?>;
