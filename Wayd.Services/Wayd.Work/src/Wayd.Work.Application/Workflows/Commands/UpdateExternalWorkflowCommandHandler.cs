using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workflows.Commands;

public sealed class UpdateExternalWorkflowCommandHandler(IWorkDbContext workDbContext, ILogger<UpdateExternalWorkflowCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<UpdateExternalWorkflowCommand>
{
    private const string AppRequestName = nameof(UpdateExternalWorkflowCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<UpdateExternalWorkflowCommandHandler> _logger = logger;
    private readonly Instant _timestamp = dateTimeProvider.Now;

    public async Task<Result> Handle(UpdateExternalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _workDbContext.Workflows
            .Include(w => w.Schemes)
                .ThenInclude(s => s.WorkStatus)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId, cancellationToken);
        if (workflow is null)
        {
            _logger.LogWarning("{AppRequestName}: workflow {WorkflowId} not found.", AppRequestName, request.WorkflowId);
            return Result.Failure($"Workflow {request.WorkflowId} not found.");
        }

        if (workflow.Name != request.Name || workflow.Description != request.Description)
        {
            var updateResult = workflow.Update(request.Name, request.Description, _timestamp);
            if (updateResult.IsFailure)
            {
                _logger.LogError("{AppRequestName}: failed to update workflow {WorkflowId}. Error: {Error}", AppRequestName, workflow.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }
        }

        var workStatuses = await _workDbContext.WorkStatuses.Where(ws => request.ExternalWorkTypeWorkflow.WorkflowStates.Select(ws => ws.StatusName).Contains(ws.Name)).ToArrayAsync(cancellationToken);
        if (workStatuses.Length < 1)
        {
            _logger.LogError("{AppRequestName}: no work statuses found.", AppRequestName);
            return Result.Failure("No work statuses found.");
        }

        foreach (var ws in request.ExternalWorkTypeWorkflow.WorkflowStates)
        {
            var workStatus = workStatuses.SingleOrDefault(s => s.Name == ws.StatusName);
            if (workStatus is null)
            {
                _logger.LogError("{AppRequestName}: work status {WorkStatusName} not found.", AppRequestName, ws.StatusName);
                return Result.Failure($"Work status {ws.StatusName} not found.");
            }

            var scheme = workflow.Schemes.SingleOrDefault(s => s.WorkStatusId == workStatus.Id);
            if (scheme is null)
            {
                var createResult = workflow.AddScheme(workStatus.Id, ws.Category, ws.Order, ws.IsActive);
                if (createResult.IsFailure)
                {
                    _logger.LogError("{AppRequestName}: failed to create scheme for work status {WorkStatusName}. Error: {Error}", AppRequestName, ws.StatusName, createResult.Error);
                    return Result.Failure(createResult.Error);
                }
            }
            else
            {
                if (scheme.WorkStatusCategory != ws.Category || scheme.Order != ws.Order)
                {
                    var updateResult = scheme.Update(ws.Category, ws.Order, _timestamp);
                    if (updateResult.IsFailure)
                    {
                        _logger.LogError("{AppRequestName}: failed to update scheme for work status {WorkStatusName}. Error: {Error}", AppRequestName, ws.StatusName, updateResult.Error);
                        return Result.Failure(updateResult.Error);
                    }
                }

                if (scheme.IsActive != ws.IsActive)
                {
                    var activateResult = ws.IsActive
                        ? workflow.ActivateScheme(scheme.Id, _timestamp)
                        : workflow.DeactivateScheme(scheme.Id, _timestamp);
                    if (activateResult.IsFailure)
                    {
                        _logger.LogError("{AppRequestName}: failed to {Action} scheme for work status {WorkStatusName}. Error: {Error}", AppRequestName, ws.IsActive ? "activate" : "deactivate", ws.StatusName, activateResult.Error);
                        return Result.Failure(activateResult.Error);
                    }
                }
            }
        }

        // handle missing schemes
        var deactivatedSchemes = workflow.Schemes
            .Where(s => s.IsActive && !request.ExternalWorkTypeWorkflow.WorkflowStates.Select(ws => ws.StatusName).Contains(s.WorkStatus!.Name))
            .ToArray();
        foreach (var scheme in deactivatedSchemes)
        {
            var deactivateResult = workflow.DeactivateScheme(scheme.Id, _timestamp);
            if (deactivateResult.IsFailure)
            {
                _logger.LogError("{AppRequestName}: failed to deactivate scheme for work status {WorkStatusName}. Error: {Error}", AppRequestName, scheme.WorkStatus!.Name, deactivateResult.Error);
                return Result.Failure(deactivateResult.Error);
            }
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("{AppRequestName}: updated workflow {WorkflowName}.", AppRequestName, workflow.Name);

        return Result.Success(workflow.Id);
    }
}
