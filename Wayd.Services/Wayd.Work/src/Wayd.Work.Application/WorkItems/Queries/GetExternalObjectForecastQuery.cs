using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// When the work items another object references (a planning interval objective, say) will all
/// be done.
/// </summary>
/// <param name="TargetDate">When the object's work is due, to report the chance of making it.</param>
public sealed record GetExternalObjectForecastQuery(Guid ObjectId, LocalDate? TargetDate, ForecastOptions Options) : IQuery<WorkItemForecastDto>;

public sealed class GetExternalObjectForecastQueryValidator : AbstractValidator<GetExternalObjectForecastQuery>
{
    public GetExternalObjectForecastQueryValidator()
    {
        RuleFor(q => q.Options).NotNull().SetValidator(new ForecastOptionsValidator());
    }
}

public sealed class GetExternalObjectForecastQueryHandler(
    IWorkDbContext workDbContext,
    IDateTimeProvider dateTimeProvider) : IQueryHandler<GetExternalObjectForecastQuery, WorkItemForecastDto>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<WorkItemForecastDto> Handle(GetExternalObjectForecastQuery request, CancellationToken cancellationToken)
    {
        var workItemIds = await _workDbContext.WorkItemReferences
            .Where(r => r.ObjectId == request.ObjectId)
            .Select(r => r.WorkItemId)
            .ToListAsync(cancellationToken);

        return await new WorkItemForecastBuilder(_workDbContext)
            .Build(workItemIds, request.ObjectId, _dateTimeProvider.Now, request.TargetDate, request.Options, cancellationToken);
    }
}
