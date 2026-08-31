using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

public sealed record CreateStatusWorkflowRequest
{
    /// <summary>
    /// The name of the status workflow.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// An optional description of the status workflow.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The key of the owner type the workflow is built for (e.g., "Release").
    /// </summary>
    public string OwnerType { get; set; } = default!;

    public CreateStatusWorkflowCommand ToCreateStatusWorkflowCommand()
    {
        return new CreateStatusWorkflowCommand(Name, Description, OwnerType);
    }
}

public sealed class CreateStatusWorkflowRequestValidator : CustomValidator<CreateStatusWorkflowRequest>
{
    public CreateStatusWorkflowRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
        RuleFor(r => r.OwnerType).NotEmpty();
    }
}
