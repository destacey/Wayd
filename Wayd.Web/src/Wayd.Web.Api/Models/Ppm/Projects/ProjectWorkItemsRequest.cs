using Wayd.Work.Application.WorkItems.Commands;

namespace Wayd.Web.Api.Models.Ppm.Projects;

/// <summary>The work items to link to, or unlink from, a project.</summary>
public sealed class ProjectWorkItemsRequest
{
    /// <summary>The work items' UUIDs, at most 100; work item keys are not accepted.</summary>
    public List<Guid> WorkItemIds { get; set; } = [];

    /// <summary>Builds the command that links the work items to project <paramref name="projectId"/>.</summary>
    public AddProjectWorkItemsCommand ToAddProjectWorkItemsCommand(Guid projectId)
        => new(projectId, WorkItemIds);

    /// <summary>Builds the command that unlinks the work items from project <paramref name="projectId"/>.</summary>
    public RemoveProjectWorkItemsCommand ToRemoveProjectWorkItemsCommand(Guid projectId)
        => new(projectId, WorkItemIds);
}

/// <summary>Checks the request carries between one and 100 work item ids.</summary>
public sealed class ProjectWorkItemsRequestValidator : CustomValidator<ProjectWorkItemsRequest>
{
    public ProjectWorkItemsRequestValidator()
    {
        RuleFor(x => x.WorkItemIds)
            .NotEmpty()
            .Must(ids => ids.Count <= AddProjectWorkItemsCommandValidator.MaxWorkItems)
                .WithMessage($"No more than {AddProjectWorkItemsCommandValidator.MaxWorkItems} work items can be changed at once.");

        RuleForEach(x => x.WorkItemIds)
            .NotEmpty();
    }
}
