using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkStatuses.Commands;

public sealed class SyncExternalWorkStatusesCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<SyncExternalWorkStatusesCommandHandler> logger) : ICommandHandler<SyncExternalWorkStatusesCommand>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SyncExternalWorkStatusesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkStatusesCommand request, CancellationToken cancellationToken)
    {
        var existingWorkStatusNames = new HashSet<string>(await _workDbContext.WorkStatuses.Select(wt => wt.Name).ToListAsync(cancellationToken));

        var workStatusesToCreate = request.WorkStatuses
            .Where(e => !existingWorkStatusNames.Contains(e.Name))
            .Select(e => WorkStatus.Create(e.Name, null, _dateTimeProvider.Now))
            .ToList();

        if (workStatusesToCreate.Count != 0)
        {
            _workDbContext.WorkStatuses.AddRange(workStatusesToCreate);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: created {WorkStatusCount} work statuses.", nameof(SyncExternalWorkStatusesCommand), workStatusesToCreate.Count);
        }
        else
        {
            _logger.LogInformation("{AppRequestName}: no new work statuses found.", nameof(SyncExternalWorkStatusesCommand));
        }

        // Work statuses are global and cannot be deleted or updated at this time

        // TODO: add the ability to disable work statuses if they are no longer used within wayd and disabled externally

        return Result.Success();
    }
}
