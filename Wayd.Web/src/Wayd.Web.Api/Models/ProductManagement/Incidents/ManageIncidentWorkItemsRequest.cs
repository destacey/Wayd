using Wayd.Common.Domain.Enums;
using Wayd.Work.Application.WorkItems.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

/// <summary>
/// Replaces every work item linked to an incident.
/// </summary>
public sealed record ManageIncidentWorkItemsRequest
{
    /// <summary>The incident id, repeated from the route.</summary>
    public Guid IncidentId { get; set; }

    /// <summary>Every work item that should stay linked; omitted ids are unlinked.</summary>
    public IEnumerable<Guid> WorkItemIds { get; set; } = [];

    /// <summary>Builds the shared work-item link command for this incident.</summary>
    public ManageExternalObjectWorkItemsCommand ToManageExternalObjectWorkItemsCommand() =>
        new(Guid.Empty, IncidentId, SystemContext.ProductManagementIncident, WorkItemIds);
}
