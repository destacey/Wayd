using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to copy a workflow scheme into a new owned one.</summary>
public sealed record CloneWorkflowSchemeRequest
{
    /// <summary>What the copy is called.</summary>
    public string Name { get; set; } = default!;

    /// <summary>An optional description; the source's when left out.</summary>
    public string? Description { get; set; }

    public CloneWorkflowSchemeCommand ToCloneWorkflowSchemeCommand(Guid id) =>
        new(id, Name, Description);
}

public sealed class CloneWorkflowSchemeRequestValidator : CustomValidator<CloneWorkflowSchemeRequest>
{
    public CloneWorkflowSchemeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
