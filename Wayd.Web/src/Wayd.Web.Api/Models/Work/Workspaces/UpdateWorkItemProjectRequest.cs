using Wayd.Work.Application.WorkItems.Commands;

namespace Wayd.Web.Api.Models.Work.Workspaces;

public class UpdateWorkItemProjectRequest
{
    /// <summary>The work item's UUID; must match `workItemId` in the route.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>The project to assign, or null to clear the work item's own project so it inherits its parent's.</summary>
    public Guid? ProjectId { get; set; }

    public UpdateWorkItemProjectCommand ToUpdateWorkItemProjectCommand(Guid workspaceId)
    {
        return new UpdateWorkItemProjectCommand(workspaceId, WorkItemId, ProjectId);
    }
}

public sealed class UpdateWorkItemProjectRequestValidator : CustomValidator<UpdateWorkItemProjectRequest>
{
    public UpdateWorkItemProjectRequestValidator()
    {
        RuleFor(c => c.WorkItemId)
            .NotEmpty();

        When(x => x.ProjectId.HasValue, () =>
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty();
        });
    }
}
