using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

public sealed record AddWorkflowStatusRequest
{
    /// <summary>
    /// The name of the status.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// An optional description of the status.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The high-level bucket the status belongs to.
    /// </summary>
    public StatusCategory Category { get; set; }

    /// <summary>
    /// The well-known meaning the status carries within its owner type, or zero for none.
    /// </summary>
    public int Alias { get; set; }

    public AddWorkflowStatusCommand ToAddWorkflowStatusCommand(Guid workflowId)
    {
        return new AddWorkflowStatusCommand(workflowId, Name, Description, Category, Alias);
    }
}

public sealed class AddWorkflowStatusRequestValidator : CustomValidator<AddWorkflowStatusRequest>
{
    public AddWorkflowStatusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
        RuleFor(r => r.Category).IsInEnum();
    }
}
