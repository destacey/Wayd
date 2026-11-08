using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

/// <summary>
/// What another module may copy of a PPM project deliverable: who it is, whose it is, what it is called and
/// where its status stands.
/// </summary>
public interface ISimpleProjectDeliverable
{
    /// <summary>The deliverable's id.</summary>
    Guid Id { get; }

    /// <summary>The project the deliverable belongs to.</summary>
    Guid ProjectId { get; }

    /// <summary>What the deliverable is called.</summary>
    string Name { get; }

    /// <summary>The category of the deliverable's current status.</summary>
    StatusCategory StatusCategory { get; }

    /// <summary>The well-known meaning of the deliverable's current status.</summary>
    ProjectDeliverableStatusAlias StatusAlias { get; }
}
