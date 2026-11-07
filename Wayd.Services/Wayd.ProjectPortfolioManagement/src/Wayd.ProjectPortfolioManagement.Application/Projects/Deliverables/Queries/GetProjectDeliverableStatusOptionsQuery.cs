using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;

/// <summary>
/// The statuses a deliverable can be moved to, with what each move needs. Null when the deliverable does not
/// exist on the project; empty when the project is completed or canceled, whose deliverables cannot move.
/// </summary>
public sealed record GetProjectDeliverableStatusOptionsQuery(ProjectIdOrKey ProjectIdOrKey, Guid DeliverableId) : IQuery<IReadOnlyList<StatusOptionDto>?>;

public sealed class GetProjectDeliverableStatusOptionsQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetProjectDeliverableStatusOptionsQuery, IReadOnlyList<StatusOptionDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IReadOnlyList<StatusOptionDto>?> Handle(GetProjectDeliverableStatusOptionsQuery request, CancellationToken cancellationToken)
    {
        var projects = _ppmDbContext.Projects.Where(request.ProjectIdOrKey.CreateFilter<Project>());

        var target = await _ppmDbContext.ProjectDeliverables
            .Where(d => d.Id == request.DeliverableId && projects.Any(p => p.Id == d.ProjectId))
            .Select(d => new
            {
                d.StatusId,
                d.StatusWorkflowId,
                ProjectClosed = projects.Any(p => p.Id == d.ProjectId
                    && (p.Status == ProjectStatus.Completed || p.Status == ProjectStatus.Canceled)),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null)
            return null;

        // Every move is refused on a closed project, so offering one would only lead to a failure.
        if (target.ProjectClosed)
            return [];

        // An empty list rather than a failure: a workflow that cannot be resolved, or one the deliverable is not
        // on yet, is an administrator's problem, and the change command reports it with the reason.
        var workflow = await _statusResolver.DeliverableWorkflow(cancellationToken);
        if (workflow.IsFailure || workflow.Value.Id != target.StatusWorkflowId)
            return [];

        return
        [
            .. workflow.Value.AllowedTargets(target.StatusId)
                .Select(s => new StatusOptionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category,
                    CategoryLabel = PpmWorkflowOwners.ProjectDeliverable.CategoryLabel(s.Category),
                    Alias = s.Alias,
                    Conditions = ProjectDeliverable.NeedsReason(s.Category)
                        ? [.. workflow.Value.ConditionsFor(target.StatusId, s.Id).Append(TransitionConditionKind.RequiresReason).Distinct().Order()]
                        : [.. workflow.Value.ConditionsFor(target.StatusId, s.Id).Order()],
                }),
        ];
    }
}
