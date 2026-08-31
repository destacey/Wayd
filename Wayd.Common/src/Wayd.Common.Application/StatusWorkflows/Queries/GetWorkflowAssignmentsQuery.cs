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

public sealed class GetWorkflowAssignmentsQueryHandler(IStatusWorkflowDbContext dbContext)
    : IQueryHandler<GetWorkflowAssignmentsQuery, List<WorkflowAssignmentDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<List<WorkflowAssignmentDto>> Handle(
        GetWorkflowAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkflowAssignments.AsNoTracking();

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
                .OrderBy(a => a.Owner.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }
}
