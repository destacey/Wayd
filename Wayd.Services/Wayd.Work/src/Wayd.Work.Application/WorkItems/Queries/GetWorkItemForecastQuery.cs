using System.Linq.Expressions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.Workspaces.Models;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// When a work item will be done. A portfolio work item is done when the last of its open
/// backlog descendants is.
/// </summary>
public sealed record GetWorkItemForecastQuery : IQuery<WorkItemForecastDto?>
{
    public GetWorkItemForecastQuery(WorkspaceIdOrKey workspaceIdOrKey, WorkItemKey workItemKey, LocalDate? targetDate = null, ForecastOptions? options = null)
    {
        WorkspaceIdOrKeyFilter = workspaceIdOrKey.CreateWorkspaceFilter<WorkItem>();
        WorkItemKey = workItemKey;
        TargetDate = targetDate;
        Options = options ?? ForecastOptions.Default;
    }

    public Expression<Func<WorkItem, bool>> WorkspaceIdOrKeyFilter { get; }
    public WorkItemKey WorkItemKey { get; }

    /// <summary>
    /// A date to report the chance of finishing by. A work item has none of its own.
    /// </summary>
    public LocalDate? TargetDate { get; }

    public ForecastOptions Options { get; }
}

public sealed class GetWorkItemForecastQueryValidator : AbstractValidator<GetWorkItemForecastQuery>
{
    public GetWorkItemForecastQueryValidator()
    {
        RuleFor(q => q.Options).SetValidator(new ForecastOptionsValidator());
    }
}

public sealed class GetWorkItemForecastQueryHandler(
    IWorkDbContext workDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<GetWorkItemForecastQueryHandler> logger) : IQueryHandler<GetWorkItemForecastQuery, WorkItemForecastDto?>
{
    private const string AppRequestName = nameof(GetWorkItemForecastQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<GetWorkItemForecastQueryHandler> _logger = logger;

    public async Task<WorkItemForecastDto?> Handle(GetWorkItemForecastQuery request, CancellationToken cancellationToken)
    {
        var workItemId = await _workDbContext.WorkItems
            .Where(w => w.Key == request.WorkItemKey)
            .Where(request.WorkspaceIdOrKeyFilter)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!workItemId.HasValue)
        {
            _logger.LogInformation("{AppRequestName} - Work item with key {WorkItemKey} not found", AppRequestName, request.WorkItemKey);
            return null;
        }

        return await new WorkItemForecastBuilder(_workDbContext)
            .Build([workItemId.Value], workItemId.Value, _dateTimeProvider.Now, request.TargetDate, request.Options, cancellationToken);
    }
}
