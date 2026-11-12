using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Incidents.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Incidents;

/// <summary>Applies a lifecycle action to an incident.</summary>
public sealed class ApplyIncidentWorkflowActionRequest
{
    /// <summary>The action to apply.</summary>
    public required IncidentWorkflowAction Action { get; init; }

    /// <summary>Why the incident was canceled, when <see cref="Action"/> is Cancel.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The instant written onto a date this action fills in. Begin responding uses it for response start
    /// when that is empty. Mark recovered uses it for recovery, and for response start when that is also empty.
    /// Empty means now. Close and cancel ignore it.
    /// </summary>
    public Instant? OccurredAt { get; init; }

    /// <summary>Builds the command for the API.</summary>
    public ApplyIncidentWorkflowActionCommand ToCommand(Guid incidentId) =>
        new(incidentId, Action, Reason, OccurredAt);
}
