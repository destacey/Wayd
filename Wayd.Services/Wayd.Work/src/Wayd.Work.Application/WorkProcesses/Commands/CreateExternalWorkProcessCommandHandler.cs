using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.Workflows;

namespace Wayd.Work.Application.WorkProcesses.Commands;

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

        var workTypeNames = externalProcess.WorkTypes.Select(wt => wt.Name).ToList();
        var workTypes = await _workDbContext.WorkTypes.Where(wt => workTypeNames.Contains(wt.Name)).ToArrayAsync(cancellationToken);

        var statusNames = externalProcess.WorkTypes.SelectMany(wt => wt.WorkflowStates).Select(s => s.StatusName).Distinct().ToList();
        var workStatuses = await _workDbContext.WorkStatuses.Where(ws => statusNames.Contains(ws.Name)).ToArrayAsync(cancellationToken);

        // Every workflow is built before anything is added to the context, so a failure part way
        // leaves nothing tracked for a later save in the same scope to write.
        var workflows = new List<(int WorkTypeId, bool IsActive, Workflow Workflow)>(externalProcess.WorkTypes.Count);
        foreach (var externalWorkType in externalProcess.WorkTypes)
        {
            var workType = workTypes.FirstOrDefault(wt => wt.Name == externalWorkType.Name);
            if (workType is null)
            {
                _logger.LogError("{AppRequestName}: work type {WorkTypeName} not found.", AppRequestName, externalWorkType.Name);
                return Result.Failure<IntegrationState<Guid>>($"Work type {externalWorkType.Name} not found.");
            }

            var workflowResult = ExternalWorkflowBuilder.Build($"{externalProcess.Name} - {externalWorkType.Name}", request.WorkflowDescription, externalWorkType, workStatuses);
            if (workflowResult.IsFailure)
            {
                _logger.LogError("{AppRequestName}: {Error}", AppRequestName, workflowResult.Error);
                return workflowResult.ConvertFailure<IntegrationState<Guid>>();
            }

            workflows.Add((workType.Id, externalWorkType.IsActive, workflowResult.Value));
        }

        var workProcess = WorkProcess.CreateExternal(externalProcess.Name, externalProcess.Description, externalProcess.Id, timestamp);
        foreach (var (workTypeId, isActive, workflow) in workflows)
        {
            _workDbContext.Workflows.Add(workflow);
            workProcess.AddWorkType(workTypeId, workflow.Id, isActive, timestamp);
        }

        // Created for a project that is being set up to sync, and a workspace needs an active work
        // process, so it starts active rather than waiting for an admin to switch it on.
        var activateResult = workProcess.Activate(WorkProcessActivationArgs.Create(EventActor.System, timestamp));
        if (activateResult.IsFailure)
            return activateResult.ConvertFailure<IntegrationState<Guid>>();

        // One save for the process and every workflow, so nothing is left behind when it fails,
        // including when a concurrent setup of the same process wins the unique external id.
        _workDbContext.WorkProcesses.Add(workProcess);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created work process {WorkProcessName}.", AppRequestName, workProcess.Name);

        return Result.Success(IntegrationState<Guid>.Create(workProcess.Id, workProcess.IsActive));
    }
}
