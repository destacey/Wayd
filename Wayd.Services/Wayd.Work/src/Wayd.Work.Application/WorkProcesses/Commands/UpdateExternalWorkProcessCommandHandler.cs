using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

/// <summary>Takes an external work process's current name and description.</summary>
public sealed class UpdateExternalWorkProcessCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateExternalWorkProcessCommandHandler> logger) : ICommandHandler<UpdateExternalWorkProcessCommand>
{
    private const string AppRequestName = nameof(UpdateExternalWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateExternalWorkProcessCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateExternalWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var workProcess = await _workDbContext.WorkProcesses
            .FirstOrDefaultAsync(wp => wp.ExternalId == request.ExternalWorkProcess.Id, cancellationToken);
        if (workProcess is null)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessExternalId} not found.", AppRequestName, request.ExternalWorkProcess.Id);
            return Result.Failure($"Work process {request.ExternalWorkProcess.Id} not found.");
        }

        var (name, description) = (workProcess.Name, workProcess.Description);
        var updateResult = workProcess.Update(request.ExternalWorkProcess.Name, request.ExternalWorkProcess.Description, _dateTimeProvider.Now);
        if (updateResult.IsFailure)
        {
            _logger.LogError("{AppRequestName}: failed to update work process {WorkProcessId}. Error: {Error}", AppRequestName, workProcess.Id, updateResult.Error);
            return Result.Failure(updateResult.Error);
        }

        // Compared after the update, which trims, so an unchanged process is not saved on every sync.
        if (workProcess.Name != name || workProcess.Description != description)
        {
            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("{AppRequestName}: updated work process {WorkProcessName}.", AppRequestName, workProcess.Name);
        }

        return Result.Success();
    }
}
