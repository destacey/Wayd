using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Interfaces;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

public sealed class UpdateExternalWorkProcessCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateExternalWorkProcessCommandHandler> logger) : ICommandHandler<UpdateExternalWorkProcessCommand>
{
    private const string AppRequestName = nameof(UpdateExternalWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly Instant _timestamp = dateTimeProvider.Now;
    private readonly ILogger<UpdateExternalWorkProcessCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateExternalWorkProcessCommand request, CancellationToken cancellationToken)
    {
        var workProcess = await _workDbContext.WorkProcesses
            .Include(wp => wp.Schemes)
            .FirstOrDefaultAsync(wp => wp.ExternalId == request.ExternalWorkProcess.Id, cancellationToken);
        if (workProcess is null)
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessExternalId} not found.", AppRequestName, request.ExternalWorkProcess.Id);
            return Result.Failure($"Work process {request.ExternalWorkProcess.Id} not found.");
        }

        if (workProcess.Name != request.ExternalWorkProcess.Name || workProcess.Description != request.ExternalWorkProcess.Description)
        {
            var updateResult = workProcess.Update(request.ExternalWorkProcess.Name, request.ExternalWorkProcess.Description, _timestamp);
            if (updateResult.IsFailure)
            {
                _logger.LogError("{AppRequestName}: failed to update work process {WorkProcessId}. Error: {Error}", AppRequestName, workProcess.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }
        }

        var syncSchemesResult = await SyncSchemes(workProcess, request.ExternalWorkTypes, request.WorkflowMappings, cancellationToken);
        if (syncSchemesResult.IsFailure)
        {
            return syncSchemesResult;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: updated work process {WorkProcessName}.", AppRequestName, workProcess.Name);

        return Result.Success();
    }

    private async Task<Result> SyncSchemes(WorkProcess workProcess, IEnumerable<IExternalWorkType> externalWorkTypes, IEnumerable<ICreateWorkProcessScheme> workflowMappings, CancellationToken cancellationToken)
    {
        var workTypes = await _workDbContext.WorkTypes.Where(wt => externalWorkTypes.Select(wt => wt.Name).Contains(wt.Name)).ToArrayAsync(cancellationToken);
        if (workTypes.Length < 1)
        {
            _logger.LogError("{AppRequestName}: no work types found.", AppRequestName);
            return Result.Failure("No work types found.");
        }

        foreach (var externalWorkType in externalWorkTypes)
        {
            var workType = workTypes.FirstOrDefault(wt => wt.Name == externalWorkType.Name);
            if (workType is null)
            {
                _logger.LogError("{AppRequestName}: work type {WorkTypeName} not found.", AppRequestName, externalWorkType.Name);
                return Result.Failure($"Work type {externalWorkType.Name} not found.");
            }

            var workflowId = workflowMappings.Single(m => m.WorkTypeName == externalWorkType.Name).WorkflowId;
            Guard.Against.Default(workflowId, nameof(workflowId));

            var scheme = workProcess.Schemes.FirstOrDefault(s => s.WorkTypeId == workType.Id);
            if (scheme is null)
            {
                var addResult = workProcess.AddWorkType(workType.Id, workflowId, externalWorkType.IsActive, _timestamp);
                if (addResult.IsFailure)
                {
                    _logger.LogError("{AppRequestName}: failed to add work type {WorkTypeId} to work process {WorkProcessId}. Error: {Error}", AppRequestName, workType.Id, workProcess.Id, addResult.Error);
                    return Result.Failure(addResult.Error);
                }
            }
            else
            {
                if (scheme.WorkflowId != workflowId)
                {
                    var updateResult = workProcess.ChangeWorkTypeWorkflow(scheme.WorkTypeId, workflowId, _timestamp);
                    if (updateResult.IsFailure)
                    {
                        _logger.LogError("{AppRequestName}: failed to change the workflow for work type on work process scheme {WorkProcessSchemeId}. Error: {Error}", AppRequestName, scheme.Id, updateResult.Error);
                        return Result.Failure(updateResult.Error);
                    }
                }

                if (!scheme.IsActive && externalWorkType.IsActive)
                {
                    var activateResult = workProcess.ActivateWorkType(scheme.WorkTypeId, _timestamp);
                    if (activateResult.IsFailure)
                    {
                        _logger.LogError("{AppRequestName}: failed to disable work type {WorkTypeId} for work process {WorkProcessId}. Error: {Error}", AppRequestName, scheme.WorkTypeId, workProcess.Id, activateResult.Error);
                        return Result.Failure(activateResult.Error);
                    }
                }
                else if (scheme.IsActive && !externalWorkType.IsActive)
                {
                    var deactivateResult = workProcess.DeactivateWorkType(scheme.WorkTypeId, _timestamp);
                    if (deactivateResult.IsFailure)
                    {
                        _logger.LogError("{AppRequestName}: failed to enable work type {WorkTypeId} for work process {WorkProcessId}. Error: {Error}", AppRequestName, scheme.WorkTypeId, workProcess.Id, deactivateResult.Error);
                        return Result.Failure(deactivateResult.Error);
                    }
                }
            }
        }

        var removedSchemes = workProcess.Schemes
            .Where(s => s.IsActive && !workTypes.Select(t => t.Id).Contains(s.WorkTypeId))
            .Select(s => s.WorkTypeId)
            .ToArray();
        foreach (var scheme in removedSchemes)
        {
            var deactivateResult = workProcess.DeactivateWorkType(scheme, _timestamp);
            if (deactivateResult.IsFailure)
            {
                _logger.LogError("{AppRequestName}: failed to disable work type {WorkTypeId} for work process {WorkProcessId}. Error: {Error}", AppRequestName, scheme, workProcess.Id, deactivateResult.Error);
                return Result.Failure(deactivateResult.Error);
            }
        }

        return Result.Success();
    }
}
