using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses.Dtos;
using Wayd.Work.Application.WorkTypes.Dtos;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.WorkProcesses.Queries;

/// <summary>Gets a work process's schemes, one per work type; null when no work process has the id.</summary>
/// <param name="WorkProcessId">The work process's id.</param>
public sealed record GetWorkProcessSchemesQuery(Guid WorkProcessId) : IQuery<IReadOnlyList<WorkProcessSchemeDto>?>;

/// <summary>Requires the work process id.</summary>
public sealed class GetWorkProcessSchemesQueryValidator : CustomValidator<GetWorkProcessSchemesQuery>
{
    public GetWorkProcessSchemesQueryValidator()
    {
        RuleFor(q => q.WorkProcessId)
            .NotEmpty();
    }
}

/// <summary>Reads a work process's work types, each with the status workflow its work items follow.</summary>
/// <remarks>
/// The work types are those with an entry in the process's managed scheme in the status workflow engine, each
/// with the workflow its entry gives it. A type that has never offered a state has no workflow and no entry, so
/// it is not listed.
/// </remarks>
public sealed class GetWorkProcessSchemesQueryHandler(IWorkDbContext workDbContext, IStatusWorkflowDbContext statusWorkflowDbContext)
    : IQueryHandler<GetWorkProcessSchemesQuery, IReadOnlyList<WorkProcessSchemeDto>?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;

    /// <summary>The work process's schemes ordered by work type name, or null when no work process has the id.</summary>
    public async Task<IReadOnlyList<WorkProcessSchemeDto>?> Handle(GetWorkProcessSchemesQuery request, CancellationToken cancellationToken)
    {
        var process = await _workDbContext.WorkProcesses
            .Where(p => p.Id == request.WorkProcessId)
            .Select(p => new { p.ExternalId })
            .FirstOrDefaultAsync(cancellationToken);
        if (process is null)
            return null;

        if (process.ExternalId is not Guid externalId)
            return [];

        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        var schemeKey = ManagedWorkflowKeys.Scheme(externalId);
        var entries = await _statusWorkflowDbContext.StatusWorkflowSchemes
            .Where(s => s.OwnerType == ownerType && s.ExternalKey == schemeKey)
            .SelectMany(s => s.Entries)
            .Select(e => new { e.Id, e.RecordTypeId, e.WorkflowId })
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            return [];

        var recordTypeIds = entries.Select(e => e.RecordTypeId).Distinct().ToList();
        var recordTypeWorkType = (await _workDbContext.WorkTypes
                .AsNoTracking()
                .Include(t => t.Level)
                .LinkedTo(recordTypeIds)
                .ToListAsync(cancellationToken))
            .DistinctBy(t => t.RecordTypeId)
            .ToDictionary(t => t.RecordTypeId!.Value, t => t.Adapt<WorkTypeDto>());

        var workflowIds = entries.Select(e => e.WorkflowId).Distinct().ToList();
        var workflows = await _statusWorkflowDbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .Where(w => workflowIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        return [.. entries
            .Where(e => recordTypeWorkType.ContainsKey(e.RecordTypeId))
            .Select(e => new WorkProcessSchemeDto
            {
                Id = e.Id,
                WorkType = recordTypeWorkType[e.RecordTypeId],
                Workflow = workflows.TryGetValue(e.WorkflowId, out var workflow) ? ToDto(workflow, ownerType) : null,
            })
            .OrderBy(s => s.WorkType.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)];
    }

    /// <summary>The workflow with its statuses, active ones first in display order and retired ones after.</summary>
    private static WorkProcessSchemeWorkflowDto ToDto(StatusWorkflow workflow, string ownerType) => new()
    {
        Id = workflow.Id,
        Key = workflow.Key,
        Name = workflow.Name,
        Statuses = [.. workflow.Statuses
            .OrderBy(s => s.IsRetired)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Id)
            .Select(s => new WorkProcessSchemeStatusDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = new SimpleNavigationDto { Id = (int)s.Category, Name = WorkflowOwners.CategoryLabel(ownerType, s.Category) },
                Order = s.Order,
                IsRetired = s.IsRetired,
            })],
    };
}
