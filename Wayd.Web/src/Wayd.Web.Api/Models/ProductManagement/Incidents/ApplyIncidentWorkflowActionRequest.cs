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

    /// <summary>Builds the command for the API.</summary>
    public ApplyIncidentWorkflowActionCommand ToCommand(Guid incidentId) =>
        new(incidentId, Action, Reason);
}
