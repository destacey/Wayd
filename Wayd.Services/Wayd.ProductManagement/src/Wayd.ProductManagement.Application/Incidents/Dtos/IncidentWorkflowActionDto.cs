using System.ComponentModel.DataAnnotations;

namespace Wayd.ProductManagement.Application.Incidents.Dtos;

/// <summary>A lifecycle action whose move an incident's workflow allows from the status it holds.</summary>
public sealed record IncidentWorkflowActionDto
{
    /// <summary>The action.</summary>
    [Required]
    public required IncidentWorkflowAction Action { get; init; }
}
