using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTypes.Commands;

public sealed class SyncExternalWorkTypesCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<SyncExternalWorkTypesCommandHandler> logger) : ICommandHandler<SyncExternalWorkTypesCommand>
{
    private const string AppRequestName = nameof(SyncExternalWorkTypesCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncExternalWorkTypesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkTypesCommand request, CancellationToken cancellationToken)
    {
        var existingWorkTypeNames = new HashSet<string>(await _workDbContext.WorkTypes.Select(wt => wt.Name).ToListAsync(cancellationToken));

        var workTypesToCreate = request.WorkTypes
            .Where(e => !existingWorkTypeNames.Contains(e.Name))
            .Select(e => WorkType.Create(e.Name, e.Description, request.DefaultWorkTypeLevelId, _dateTimeProvider.Now))
            .ToList();

        if (workTypesToCreate.Count > 0)
        {
            _workDbContext.WorkTypes.AddRange(workTypesToCreate);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: created {WorkTypeCount} work types.", AppRequestName, workTypesToCreate.Count);
        }
        else
        {
            _logger.LogInformation("{AppRequestName}: no new work types found.", AppRequestName);
        }

        // Work types are global and cannot be deleted or updated at this time

        // TODO: add the ability to disable work types if they are no longer used within wayd and disabled externally

        return Result.Success();
    }
}
