using Wayd.Common.Extensions;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record InitWorkspaceIntegrationRequest
{
    /// <summary>
    /// Connection Id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// External identifier for the workspace.
    /// </summary>
    public Guid ExternalId { get; set; }

    /// <summary>
    /// The key for the workspace.
    /// </summary>
    public string WorkspaceKey { get; set; } = default!;

    /// <summary>
    /// The name for the workspace.
    /// </summary>
    public string WorkspaceName { get; set; } = default!;

    /// <summary>
    /// A url template for external work items.  This template plus the work item external id will create a url to view the work item in the external system.
    /// </summary>
    public string? ExternalViewWorkItemUrlTemplate { get; set; }
}

public sealed class InitWorkspaceIntegrationRequestValidator : CustomValidator<InitWorkspaceIntegrationRequest>
{
    public InitWorkspaceIntegrationRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Id)
            .NotEmpty();

        RuleFor(t => t.ExternalId)
            .NotEmpty();

        RuleFor(t => t.WorkspaceKey)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(20)
            .Must(t => t.IsValidWorkspaceKeyFormat())
                .WithMessage("Invalid workspace key format. Workspace key's must be uppercase letters and numbers only and start with an uppercase letter.");

        RuleFor(c => c.WorkspaceName)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.ExternalViewWorkItemUrlTemplate)
            .MaximumLength(256)
            .IsWebAddress();
    }
}
