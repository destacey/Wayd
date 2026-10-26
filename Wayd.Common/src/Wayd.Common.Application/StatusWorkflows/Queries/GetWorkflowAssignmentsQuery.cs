using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// Which workflow governs each scope.
/// </summary>
/// <remarks>
/// One row per owner type today: every consumer assigns organization-wide, so the scope is always null.
/// The shape allows for narrower scopes because the domain does.
/// </remarks>
public sealed record GetWorkflowAssignmentsQuery(string? OwnerType) : IQuery<List<WorkflowAssignmentDto>>;

public sealed class GetWorkflowAssignmentsQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<GetWorkflowAssignmentsQuery, List<WorkflowAssignmentDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<List<WorkflowAssignmentDto>> Handle(
        GetWorkflowAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var unavailable = (await _ownerAvailability.Unavailable(cancellationToken)).ToArray();

        var query = _dbContext.WorkflowAssignments.AsNoTracking()
            .Where(a => !unavailable.Contains(a.OwnerType));

        if (!string.IsNullOrWhiteSpace(request.OwnerType))
        {
            query = query.Where(a => a.OwnerType == request.OwnerType);
        }

        var assignments = await query
            .Join(
                _dbContext.StatusWorkflows.AsNoTracking(),
                assignment => assignment.WorkflowId,
                workflow => workflow.Id,
                (assignment, workflow) => new
                {
                    assignment.Id,
                    assignment.OwnerType,
                    assignment.ScopeId,
                    assignment.WorkflowId,
                    WorkflowName = workflow.Name,
                    WorkflowKey = workflow.Key,
                })
            .ToListAsync(cancellationToken);

        return
        [
            .. assignments
                .Select(a => new WorkflowAssignmentDto
                {
                    Id = a.Id,
                    Owner = GetStatusWorkflowsQueryHandler.DescribeOwner(a.OwnerType),
                    ScopeId = a.ScopeId,
                    Workflow = new NavigationDto
                    {
                        Id = a.WorkflowId,
                        Key = a.WorkflowKey,
                        Name = a.WorkflowName,
                    },
                })
                // The organization's assignment first within an owner type, then each scope's, ending on the id so
                // the order is the same on every call.
                .OrderBy(a => a.Owner.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.ScopeId is not null)
                .ThenBy(a => a.ScopeId)
                .ThenBy(a => a.Id),
        ];
    }
}
