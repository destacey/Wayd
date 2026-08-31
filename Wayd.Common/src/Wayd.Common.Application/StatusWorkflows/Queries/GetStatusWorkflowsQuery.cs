using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <param name="OwnerType">Limit to one owner type, or null for all.</param>
/// <param name="State">Limit to one lifecycle state, or null for all.</param>
public sealed record GetStatusWorkflowsQuery(string? OwnerType, StatusWorkflowState? State)
    : IQuery<List<StatusWorkflowListDto>>;

public sealed class GetStatusWorkflowsQueryHandler(IStatusWorkflowDbContext dbContext)
    : IQueryHandler<GetStatusWorkflowsQuery, List<StatusWorkflowListDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    public async Task<List<StatusWorkflowListDto>> Handle(
        GetStatusWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.StatusWorkflows.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.OwnerType))
        {
            query = query.Where(w => w.OwnerType == request.OwnerType);
        }

        if (request.State is not null)
        {
            query = query.Where(w => w.State == request.State);
        }

        // Assigned workflows are read in one go rather than per row: the set is tiny (one assignment
        // per owner type today) and a correlated subquery per workflow would be pure ceremony.
        var assignedIds = await _dbContext.WorkflowAssignments
            .Select(a => a.WorkflowId)
            .ToListAsync(cancellationToken);

        var assigned = assignedIds.ToHashSet();

        var workflows = await query
            .Select(w => new
            {
                w.Id,
                w.Key,
                w.Name,
                w.Description,
                w.OwnerType,
                w.State,
                w.IsSystem,
                StatusCount = w.Statuses.Count,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. workflows
                .Select(w => new StatusWorkflowListDto
                {
                    Id = w.Id,
                    Key = w.Key,
                    Name = w.Name,
                    Description = w.Description,
                    Owner = DescribeOwner(w.OwnerType),
                    State = w.State.ToString(),
                    IsSystem = w.IsSystem,
                    StatusCount = w.StatusCount,
                    IsAssigned = assigned.Contains(w.Id),
                })
                .OrderBy(w => w.Owner.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// The owner type's display name, falling back to its key.
    /// </summary>
    /// <remarks>
    /// A workflow can outlive its descriptor — a module removed from a build leaves its rows behind —
    /// and showing the raw key is more honest there than hiding the row.
    /// </remarks>
    internal static WorkflowOwnerNavigationDto DescribeOwner(string ownerType)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);

        return new WorkflowOwnerNavigationDto
        {
            Key = ownerType,
            Name = descriptor.IsSuccess ? descriptor.Value.DisplayName : ownerType,
        };
    }
}
