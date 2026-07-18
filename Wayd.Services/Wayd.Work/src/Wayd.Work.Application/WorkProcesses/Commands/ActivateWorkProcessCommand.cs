using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

public sealed record ActivateWorkProcessCommand(Guid Id) : ICommand;

public sealed class ActivateWorkProcessCommandHandler(IWorkDbContext workDbContext, ILogger<ActivateWorkProcessCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ActivateWorkProcessCommand>
{
    private const string AppRequestName = nameof(ActivateWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<ActivateWorkProcessCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ActivateWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var workProcess = await _workDbContext.WorkProcesses.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (workProcess is null)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessId} not found.", AppRequestName, request.Id);
            return Result.Failure($"Work process {request.Id} not found.");
        }

        if (workProcess.IsActive)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessName} ({WorkProcessId}) is already active.", AppRequestName, workProcess.Name, workProcess.Id);
            return Result.Failure($"Work process {workProcess.Name} is already active.");
        }

        workProcess.Activate(_dateTimeProvider.Now);

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: activated work process {WorkProcessName} ({WorkProcessId}).", AppRequestName, workProcess.Name, workProcess.Id);

        return Result.Success();
    }
}
