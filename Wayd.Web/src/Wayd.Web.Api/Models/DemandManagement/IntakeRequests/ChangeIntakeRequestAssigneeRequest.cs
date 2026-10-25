namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// The member of an intake request's desk who owns it.
/// </summary>
public sealed record ChangeIntakeRequestAssigneeRequest
{
    /// <summary>
    /// A member of the request's desk, or null to unassign it.
    /// </summary>
    public Guid? AssigneeId { get; set; }
}

public sealed class ChangeIntakeRequestAssigneeRequestValidator : CustomValidator<ChangeIntakeRequestAssigneeRequest>
{
    public ChangeIntakeRequestAssigneeRequestValidator()
    {
        RuleFor(r => r.AssigneeId).NotEqual(Guid.Empty);
    }
}
