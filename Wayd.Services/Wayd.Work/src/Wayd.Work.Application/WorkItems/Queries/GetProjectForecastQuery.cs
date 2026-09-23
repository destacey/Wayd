using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// When the work items in a project will all be done: those assigned to it directly, or through
/// a parent when they have no project of their own.
/// </summary>
/// <param name="TargetDate">When the project is due, to report the chance of making it.</param>
public sealed record GetProjectForecastQuery(Guid ProjectId, LocalDate? TargetDate, ForecastOptions Options) : IQuery<WorkItemForecastDto>;

public sealed class GetProjectForecastQueryValidator : AbstractValidator<GetProjectForecastQuery>
{
    public GetProjectForecastQueryValidator()
    {
        RuleFor(q => q.Options).NotNull().SetValidator(new ForecastOptionsValidator());
    }
}

public sealed class GetProjectForecastQueryHandler(
    IWorkDbContext workDbContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetProjectForecastQuery, WorkItemForecastDto>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<WorkItemForecastDto> Handle(GetProjectForecastQuery request, CancellationToken cancellationToken)
    {
        // The same membership GetProjectWorkItemsQuery uses.
        var workItemIds = await _workDbContext.WorkItems
            .Where(w => (w.ProjectId != null && w.ProjectId == request.ProjectId)
                || (w.ProjectId == null && w.ParentProjectId != null && w.ParentProjectId == request.ProjectId))
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        return await new WorkItemForecastBuilder(_workDbContext)
            .Build(workItemIds, request.ProjectId, _dateTimeProvider.Now, request.TargetDate, request.Options, cancellationToken);
    }
}
