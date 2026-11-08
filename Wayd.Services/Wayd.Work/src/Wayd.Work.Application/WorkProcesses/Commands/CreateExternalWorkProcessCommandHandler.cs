using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

/// <summary>Creates the Wayd work process for an external process, active.</summary>
public sealed class CreateExternalWorkProcessCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateExternalWorkProcessCommandHandler> logger) : ICommandHandler<CreateExternalWorkProcessCommand, IntegrationState<Guid>>
{
    private const string AppRequestName = nameof(CreateExternalWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateExternalWorkProcessCommandHandler> _logger = logger;

    public async Task<Result<IntegrationState<Guid>>> Handle(CreateExternalWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var externalProcess = request.ExternalWorkProcess;

        if (await _workDbContext.WorkProcesses.AnyAsync(wp => wp.ExternalId == externalProcess.Id, cancellationToken))
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessName} already exists.", AppRequestName, externalProcess.Name);
            return Result.Failure<IntegrationState<Guid>>($"Work process {externalProcess.Name} already exists.");
        }

        var timestamp = _dateTimeProvider.Now;
        var workProcess = WorkProcess.CreateExternal(externalProcess.Name, externalProcess.Description, externalProcess.Id, timestamp);

        // Created for a project that is being set up to sync, and a workspace needs an active work
        // process, so it starts active rather than waiting for an admin to switch it on.
        var activateResult = workProcess.Activate(WorkProcessActivationArgs.Create(EventActor.System, timestamp));
        if (activateResult.IsFailure)
            return activateResult.ConvertFailure<IntegrationState<Guid>>();

        // A concurrent setup of the same process fails here on the unique external id; the next attempt links
        // to the winner.
        _workDbContext.WorkProcesses.Add(workProcess);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created work process {WorkProcessName}.", AppRequestName, workProcess.Name);

        return Result.Success(IntegrationState<Guid>.Create(workProcess.Id, workProcess.IsActive));
    }
}
