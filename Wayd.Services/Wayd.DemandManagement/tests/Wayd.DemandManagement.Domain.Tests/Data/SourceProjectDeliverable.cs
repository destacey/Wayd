using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// A PPM project deliverable as its source reports it, for building and resyncing a <c>DemandProjectDeliverable</c>.
/// </summary>
public sealed record SourceProjectDeliverable(
    Guid Id,
    Guid ProjectId,
    string Name,
    StatusCategory StatusCategory = StatusCategory.Proposed,
    ProjectDeliverableStatusAlias StatusAlias = ProjectDeliverableStatusAlias.NotStarted) : ISimpleProjectDeliverable
{
    public static SourceProjectDeliverable Any(Guid? projectId = null, string name = "Payments API") =>
        new(Guid.CreateVersion7(), projectId ?? Guid.CreateVersion7(), name);

    public static SourceProjectDeliverable Accepted(Guid projectId, string name = "Payments API") =>
        new(Guid.CreateVersion7(), projectId, name, StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted);
}
