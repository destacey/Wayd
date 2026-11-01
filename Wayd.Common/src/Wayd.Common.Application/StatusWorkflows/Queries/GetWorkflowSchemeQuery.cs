using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>One workflow scheme with its entries and the scopes pointing at it.</summary>
public sealed record GetWorkflowSchemeQuery : IQuery<WorkflowSchemeDetailsDto?>
{
    public GetWorkflowSchemeQuery(IdOrKey idOrKey) => IdOrKeyFilter = idOrKey.CreateFilter<StatusWorkflowScheme>();

    public Expression<Func<StatusWorkflowScheme, bool>> IdOrKeyFilter { get; }
}

public sealed class GetWorkflowSchemeQueryHandler(
    IStatusWorkflowDbContext dbContext,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<GetWorkflowSchemeQuery, WorkflowSchemeDetailsDto?>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<WorkflowSchemeDetailsDto?> Handle(GetWorkflowSchemeQuery request, CancellationToken cancellationToken)
    {
        var scheme = await _dbContext.StatusWorkflowSchemes.AsNoTracking()
            .Include(s => s.Entries)
            .Where(request.IdOrKeyFilter)
            .FirstOrDefaultAsync(s => !s.IsImplicit, cancellationToken);

        if (scheme is null || !await _ownerAvailability.IsAvailable(scheme.OwnerType, cancellationToken))
        {
            return null;
        }

        var workflowIds = scheme.Entries.Select(e => e.WorkflowId).Append(scheme.DefaultWorkflowId).Distinct().ToList();
        var workflows = await _dbContext.StatusWorkflows.AsNoTracking()
            .Where(w => workflowIds.Contains(w.Id))
            .Select(w => new WorkflowSchemeWorkflowDto { Id = w.Id, Key = w.Key, Name = w.Name })
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        var recordTypeIds = scheme.Entries.Select(e => e.RecordTypeId).ToList();
        var recordTypes = await _dbContext.RecordTypes.AsNoTracking()
            .Where(r => recordTypeIds.Contains(r.Id))
            .Select(r => new RecordTypeNavigationDto { Id = r.Id, Key = r.Key, Name = r.Name })
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var scopeIds = await _dbContext.AssignedScopes(scheme.Id, cancellationToken);

        return new WorkflowSchemeDetailsDto
        {
            Id = scheme.Id,
            Key = scheme.Key,
            Name = scheme.Name,
            Description = scheme.Description,
            Owner = GetStatusWorkflowsQueryHandler.DescribeOwner(scheme.OwnerType),
            Ownership = scheme.Ownership,
            ExternalKey = scheme.ExternalKey,
            DefaultWorkflow = workflows[scheme.DefaultWorkflowId],
            Entries =
            [
                .. scheme.Entries
                    .Select(e => new WorkflowSchemeEntryDto { RecordType = recordTypes[e.RecordTypeId], Workflow = workflows[e.WorkflowId] })
                    .OrderBy(e => e.RecordType.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.RecordType.Key),
            ],
            AssignedScopeIds = [.. scopeIds.OrderBy(id => id.HasValue).ThenBy(id => id)],
        };
    }
}
