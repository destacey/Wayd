using System.ComponentModel.DataAnnotations;

namespace Wayd.ProductManagement.Application.DeliveryWorkflows;

/// <summary>
/// A lifecycle action whose move a record's workflow allows from the status the record holds.
/// </summary>
public sealed record DeliveryWorkflowActionDto
{
    /// <summary>The action.</summary>
    [Required]
    public required DeliveryAction Action { get; init; }
}
