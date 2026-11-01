using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to edit a workflow scheme's name and description.</summary>
public sealed record UpdateWorkflowSchemeRequest
{
    /// <summary>What the scheme is called.</summary>
    public string Name { get; set; } = default!;

    /// <summary>An optional explanation shown to administrators choosing between schemes.</summary>
    public string? Description { get; set; }

    public UpdateWorkflowSchemeCommand ToUpdateWorkflowSchemeCommand(Guid id) =>
        new(id, Name, Description);
}

public sealed class UpdateWorkflowSchemeRequestValidator : CustomValidator<UpdateWorkflowSchemeRequest>
{
    public UpdateWorkflowSchemeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
