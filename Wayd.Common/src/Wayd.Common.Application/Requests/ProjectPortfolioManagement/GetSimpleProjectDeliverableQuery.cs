using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Requests.ProjectPortfolioManagement;

/// <summary>
/// The current state of one PPM project deliverable, or null when it no longer exists. Read by Demand
/// Management, which keeps a copy of each deliverable, when an event reaches it for a deliverable it has no
/// copy of.
/// </summary>
public sealed record GetSimpleProjectDeliverableQuery(Guid Id) : IQuery<ISimpleProjectDeliverable?>;

/// <summary>
/// The current state of every PPM project deliverable, for the Projects Sync job's repair of the copies.
/// </summary>
public sealed record GetSimpleProjectDeliverablesQuery() : IQuery<List<ISimpleProjectDeliverable>>;

/// <summary>A PPM project deliverable as its source reports it to another module.</summary>
public sealed record SimpleProjectDeliverable(
    Guid Id,
    Guid ProjectId,
    string Name,
    StatusCategory StatusCategory,
    ProjectDeliverableStatusAlias StatusAlias) : ISimpleProjectDeliverable;
