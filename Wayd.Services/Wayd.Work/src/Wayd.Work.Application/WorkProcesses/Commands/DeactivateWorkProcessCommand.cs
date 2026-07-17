using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

public sealed record DeactivateWorkProcessCommand(Guid Id) : ICommand;

public sealed class DeactivateWorkProcessCommandHandler(IWorkDbContext workDbContext, ILogger<DeactivateWorkProcessCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<DeactivateWorkProcessCommand>
{
    private const string AppRequestName = nameof(DeactivateWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<DeactivateWorkProcessCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeactivateWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var workProcess = await _workDbContext.WorkProcesses.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (workProcess is null)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessId} not found.", AppRequestName, request.Id);
            return Result.Failure($"Work process {request.Id} not found.");
        }

        if (!workProcess.IsActive)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessName} ({WorkProcessId}) is already inactive.", AppRequestName, workProcess.Name, workProcess.Id);
            return Result.Failure($"Work process {workProcess.Name} is already inactive.");
        }

        workProcess.Deactivate(_dateTimeProvider.Now);

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: deactivated work process {WorkProcessName} ({WorkProcessId}).", AppRequestName, workProcess.Name, workProcess.Id);

        return Result.Success();
    }
}
