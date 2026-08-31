using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to copy an existing workflow into a new editable draft.
/// </summary>
public sealed record CloneStatusWorkflowRequest
{
    /// <summary>
    /// The name of the new draft workflow.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// An optional description of the new draft workflow.
    /// </summary>
    public string? Description { get; set; }

    public CloneStatusWorkflowCommand ToCloneStatusWorkflowCommand(Guid id)
    {
        return new CloneStatusWorkflowCommand(id, Name, Description);
    }
}

public sealed class CloneStatusWorkflowRequestValidator : CustomValidator<CloneStatusWorkflowRequest>
{
    public CloneStatusWorkflowRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
