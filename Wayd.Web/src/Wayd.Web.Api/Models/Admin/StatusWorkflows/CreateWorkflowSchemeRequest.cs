using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to create a workflow scheme for an owner type with record types.</summary>
public sealed record CreateWorkflowSchemeRequest
{
    /// <summary>The owner type whose records the scheme sends to workflows.</summary>
    public string OwnerType { get; set; } = default!;

    /// <summary>What the scheme is called.</summary>
    public string Name { get; set; } = default!;

    /// <summary>An optional explanation shown to administrators choosing between schemes.</summary>
    public string? Description { get; set; }

    /// <summary>The published workflow records follow when their type has no entry.</summary>
    public Guid DefaultWorkflowId { get; set; }

    public CreateWorkflowSchemeCommand ToCreateWorkflowSchemeCommand() =>
        new(OwnerType, Name, Description, DefaultWorkflowId);
}

public sealed class CreateWorkflowSchemeRequestValidator : CustomValidator<CreateWorkflowSchemeRequest>
{
    public CreateWorkflowSchemeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
        RuleFor(r => r.DefaultWorkflowId).NotEmpty();
    }
}
