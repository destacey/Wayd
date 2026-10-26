namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// The review workflow a desk's requests should follow.
/// </summary>
public sealed record ChangeIntakeDeskWorkflowRequest
{
    /// <summary>A published intake request workflow, from the workflow choices.</summary>
    public Guid WorkflowId { get; set; }
}

public sealed class ChangeIntakeDeskWorkflowRequestValidator : CustomValidator<ChangeIntakeDeskWorkflowRequest>
{
    public ChangeIntakeDeskWorkflowRequestValidator()
    {
        RuleFor(r => r.WorkflowId).NotEmpty();
    }
}
