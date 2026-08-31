using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

public sealed record RenameWorkflowStatusRequest
{
    /// <summary>
    /// The name of the status.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// An optional description of the status.
    /// </summary>
    public string? Description { get; set; }

    public RenameWorkflowStatusCommand ToRenameWorkflowStatusCommand(Guid workflowId, Guid statusId)
    {
        return new RenameWorkflowStatusCommand(workflowId, statusId, Name, Description);
    }
}

public sealed class RenameWorkflowStatusRequestValidator : CustomValidator<RenameWorkflowStatusRequest>
{
    public RenameWorkflowStatusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
