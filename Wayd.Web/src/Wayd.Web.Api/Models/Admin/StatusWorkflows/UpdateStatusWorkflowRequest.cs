using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

public sealed record UpdateStatusWorkflowRequest
{
    /// <summary>
    /// The name of the status workflow.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// An optional description of the status workflow.
    /// </summary>
    public string? Description { get; set; }

    public UpdateStatusWorkflowCommand ToUpdateStatusWorkflowCommand(Guid id)
    {
        return new UpdateStatusWorkflowCommand(id, Name, Description);
    }
}

public sealed class UpdateStatusWorkflowRequestValidator : CustomValidator<UpdateStatusWorkflowRequest>
{
    public UpdateStatusWorkflowRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
