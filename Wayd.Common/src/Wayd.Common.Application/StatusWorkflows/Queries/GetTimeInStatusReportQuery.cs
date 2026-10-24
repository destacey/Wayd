using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// How long records of one owner type spent in each status, for stays that ended within a window.
/// </summary>
/// <param name="OwnerType">
/// The registered owner type. Supplied by the module's endpoint, never by the caller, so a request
/// reads only the record type its route names.
/// </param>
/// <param name="From">Start of the window, inclusive.</param>
/// <param name="To">End of the window, inclusive.</param>
public sealed record GetTimeInStatusReportQuery(string OwnerType, Instant From, Instant To)
    : IQuery<Result<TimeInStatusReportDto>>;

/// <summary>Refuses a window that ends before it starts.</summary>
public sealed class GetTimeInStatusReportQueryValidator : AbstractValidator<GetTimeInStatusReportQuery>
{
    public GetTimeInStatusReportQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The end of the window cannot be before its start.");
    }
}

/// <summary>
/// Builds the time in status report from the shared transition log.
/// </summary>
/// <remarks>
/// Stays that began or ended with a workflow reassignment are left out. A reassignment ends every open
/// stay at one instant and starts the next one there, so neither length measures the status — counting
/// them would put a burst of artificial stays in the window holding the reassignment.
/// </remarks>
public sealed class GetTimeInStatusReportQueryHandler(IStatusWorkflowDbContext dbContext)
    : IQueryHandler<GetTimeInStatusReportQuery, Result<TimeInStatusReportDto>>
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;

    /// <summary>The report, or a failure when the owner type is not registered.</summary>
    public async Task<Result<TimeInStatusReportDto>> Handle(
        GetTimeInStatusReportQuery request, CancellationToken cancellationToken)
    {
        var owner = WorkflowOwners.Resolve(request.OwnerType);
        if (owner.IsFailure)
        {
            return Result.Failure<TimeInStatusReportDto>(owner.Error);
        }

        var ownerType = owner.Value.Key;
        var workflow = await LoadGoverningWorkflow(ownerType, cancellationToken);

        // The records that changed status in the window: a stay that ends in the window ends with one of
        // their transitions. Found first, through the (OwnerType, ChangedOn) index, so the read touches
        // only those records rather than the owner type's whole history.
        var recordsChangedInWindow = _dbContext.StatusTransitions
            .Where(t => t.OwnerType == ownerType && t.ChangedOn >= request.From && t.ChangedOn <= request.To)
            .Select(t => t.RecordId)
            .Distinct();

        // Every entry of those records up to the window's end: a stay ending in the window may have begun
        // before it opened. Entries after the window are left out, so a stay that ends after it reads as
        // still open and is skipped.
        var entries = await _dbContext.StatusTransitions
            .AsNoTracking()
            .Where(t => t.OwnerType == ownerType
                && t.ChangedOn <= request.To
                && recordsChangedInWindow.Contains(t.RecordId))
            .Select(t => new
            {
                t.RecordId,
                t.Sequence,
                t.WorkflowId,
                t.ToStatusId,
                t.ToStatusName,
                t.ToCategory,
                t.ChangedOn,
            })
            .ToListAsync(cancellationToken);

        var visits = entries
            .GroupBy(e => e.RecordId)
            .SelectMany(record => TimeInStatus.Calculate(
                record.Select(e => new StatusEntry(
                    e.Sequence, e.WorkflowId, e.ToStatusId, e.ToStatusName, e.ToCategory, e.ChangedOn)),
                request.To).Visits)
            .Where(v => v.LeftOn is { } left && left >= request.From && left <= request.To)
            .Where(v => !v.TouchesWorkflowSwitch)
            .ToList();

        var byStatus = visits
            .GroupBy(v => v.StatusId)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.LeftOn).ToList());

        var rows = new List<TimeInStatusReportRowDto>();

        foreach (var status in workflow?.Statuses ?? [])
        {
            byStatus.Remove(status.Id, out var stays);
            rows.Add(Row(status.Id, status.Name, status.Category, true, stays ?? []));
        }

        // Statuses the current workflow does not have, reported under the name frozen on their latest
        // stay so history from before a reassignment keeps its own statuses.
        rows.AddRange(byStatus
            .Select(kv => Row(kv.Key, kv.Value[^1].StatusName, kv.Value[^1].Category, false, kv.Value))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase));

        return new TimeInStatusReportDto
        {
            From = request.From,
            To = request.To,
            Workflow = workflow is null ? null : NavigationDto.Create(workflow.Id, workflow.Key, workflow.Name),
            Statuses = rows,
        };
    }

    /// <summary>
    /// The workflow assigned organization-wide to this owner type, with its statuses.
    /// </summary>
    private async Task<StatusWorkflow?> LoadGoverningWorkflow(string ownerType, CancellationToken cancellationToken)
    {
        var workflowId = await _dbContext.WorkflowAssignments
            .AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.ScopeId == null)
            .Select(a => (Guid?)a.WorkflowId)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflowId is null)
        {
            return null;
        }

        return await _dbContext.StatusWorkflows
            .AsNoTracking()
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);
    }

    private static TimeInStatusReportRowDto Row(
        Guid statusId,
        string name,
        StatusCategory category,
        bool inCurrentWorkflow,
        List<StatusVisit> stays)
    {
        var seconds = stays.Select(s => s.Duration.TotalSeconds).Order().ToList();
        double? median = seconds.Count == 0 ? null : Median(seconds);

        return new TimeInStatusReportRowDto
        {
            StatusId = statusId,
            Name = name,
            Category = category,
            InCurrentWorkflow = inCurrentWorkflow,
            Visits = seconds.Count,
            AverageSeconds = seconds.Count == 0 ? null : StatusHistoryReader.WholeSeconds(Duration.FromSeconds(seconds.Average())),
            MedianSeconds = median is null ? null : StatusHistoryReader.WholeSeconds(Duration.FromSeconds(median.Value)),
        };
    }

    /// <summary>The middle value of an ascending list, or the mean of the middle two when even.</summary>
    private static double Median(List<double> ascending)
    {
        var middle = ascending.Count / 2;

        return ascending.Count % 2 == 1
            ? ascending[middle]
            : (ascending[middle - 1] + ascending[middle]) / 2;
    }
}
