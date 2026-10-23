namespace Wayd.Web.Api.Models.Work.Workspaces;

/// <summary>Sets or clears a Managed workspace's external view work item URL template.</summary>
/// <param name="ExternalViewWorkItemUrlTemplate">The URL prefix a work item's external id is appended to; null, empty or whitespace clears it.</param>
public sealed record SetExternalUrlTemplatesRequest(string? ExternalViewWorkItemUrlTemplate);

public sealed class SetExternalUrlTemplatesRequestValidator : CustomValidator<SetExternalUrlTemplatesRequest>
{
    public SetExternalUrlTemplatesRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ExternalViewWorkItemUrlTemplate)
            .MaximumLength(256);
    }
}
