using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// The workflow schemes of one owner type, or of every available one. Implicit default-only schemes are
/// left out: administrators pick their workflows directly.
/// </summary>
public sealed record GetWorkflowSchemesQuery(string? OwnerType) : IQuery<List<WorkflowSchemeListDto>>;

public sealed class GetWorkflowSchemesQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<GetWorkflowSchemesQuery, List<WorkflowSchemeListDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<List<WorkflowSchemeListDto>> Handle(GetWorkflowSchemesQuery request, CancellationToken cancellationToken)
    {
        var unavailable = (await _ownerAvailability.Unavailable(cancellationToken)).ToArray();

        var query = _dbContext.StatusWorkflowSchemes.AsNoTracking()
            .Where(s => !s.IsImplicit && !unavailable.Contains(s.OwnerType));
        if (!string.IsNullOrWhiteSpace(request.OwnerType))
        {
            query = query.Where(s => s.OwnerType == request.OwnerType);
        }

        var schemes = await query
            .Join(_dbContext.StatusWorkflows, s => s.DefaultWorkflowId, w => w.Id, (s, w) => new
            {
                s.Id,
                s.Key,
                s.Name,
                s.Description,
                s.OwnerType,
                s.Ownership,
                DefaultWorkflow = new WorkflowSchemeWorkflowDto { Id = w.Id, Key = w.Key, Name = w.Name },
                EntryCount = s.Entries.Count,
                AssignedScopeCount = _dbContext.WorkflowAssignments.Count(a => a.SchemeId == s.Id),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. schemes
                .Select(s => new WorkflowSchemeListDto
                {
                    Id = s.Id,
                    Key = s.Key,
                    Name = s.Name,
                    Description = s.Description,
                    Owner = GetStatusWorkflowsQueryHandler.DescribeOwner(s.OwnerType),
                    Ownership = s.Ownership,
                    DefaultWorkflow = s.DefaultWorkflow,
                    EntryCount = s.EntryCount,
                    AssignedScopeCount = s.AssignedScopeCount,
                })
                .OrderBy(s => s.Owner.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Key),
        ];
    }
}
