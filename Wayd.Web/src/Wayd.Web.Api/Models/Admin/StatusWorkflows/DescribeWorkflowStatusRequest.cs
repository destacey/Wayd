using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to change only a status's description, leaving its name as it is.
/// </summary>
public sealed record DescribeWorkflowStatusRequest
{
    /// <summary>
    /// What the status means, shown when picking it. Empty or omitted clears it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>The command describing the status <paramref name="statusId"/> of workflow <paramref name="workflowId"/>.</summary>
    public DescribeWorkflowStatusCommand ToDescribeWorkflowStatusCommand(Guid workflowId, Guid statusId)
    {
        return new DescribeWorkflowStatusCommand(workflowId, statusId, Description);
    }
}

/// <summary>Caps the description at the length a status stores.</summary>
public sealed class DescribeWorkflowStatusRequestValidator : CustomValidator<DescribeWorkflowStatusRequest>
{
    public DescribeWorkflowStatusRequestValidator()
    {
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
