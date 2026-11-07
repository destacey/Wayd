using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.WorkProcesses.Commands;

/// <summary>
/// Keeps a work process's record types, workflows and scheme in the engine, and assigns the scheme to every
/// workspace on the process.
/// </summary>
/// <remarks>
/// Each piece is saved by its own engine command, so a failure part-way leaves what was already synced in
/// place; the next run finds it by key and carries on.
/// <para>
/// A work type the process disables keeps its record type, which is organization-wide and may still be in
/// use by another process. A work type with no active states has no workflow to give its records and is
/// left out of the scheme.
/// </para>
/// </remarks>
public sealed class SyncExternalWorkProcessWorkflowsCommandHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ILogger<SyncExternalWorkProcessWorkflowsCommandHandler> logger)
    : ICommandHandler<SyncExternalWorkProcessWorkflowsCommand>
{
    private const string AppRequestName = nameof(SyncExternalWorkProcessWorkflowsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<SyncExternalWorkProcessWorkflowsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SyncExternalWorkProcessWorkflowsCommand request, CancellationToken cancellationToken)
    {
        var process = request.WorkProcess;
        var ownerType = WorkWorkflowOwners.WorkItem.Key;

        var workProcessId = await _workDbContext.WorkProcesses
            .Where(p => p.ExternalId == process.Id)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (workProcessId is null)
        {
            _logger.LogWarning("{AppRequestName}: work process with external ID {WorkProcessExternalId} not found.", AppRequestName, process.Id);
            return Result.Failure($"Work process with external ID {process.Id} not found.");
        }

        var entries = new List<(string WorkTypeName, ManagedSchemeEntry Entry)>();
        foreach (var workType in process.WorkTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var statuses = SourceStatuses(workType);
            if (statuses.All(s => s.IsHidden))
            {
                _logger.LogWarning("{AppRequestName}: work type {WorkType} in process {WorkProcessName} has only hidden states; it is left out of the scheme.",
                    AppRequestName, workType.Name, process.Name);
                continue;
            }

            var recordTypeResult = await _dispatcher.Send(new SyncManagedRecordTypeCommand(
                ownerType, ManagedWorkflowKeys.RecordType(workType.Name), workType.Name.Trim()), cancellationToken);
            if (recordTypeResult.IsFailure)
                return Failed("record type", workType.Name, recordTypeResult.Error);

            var workflowResult = await _dispatcher.Send(new SyncManagedWorkflowCommand(
                ownerType,
                ManagedWorkflowKeys.Workflow(process.Id, workType.ExternalKey),
                $"{process.Name} - {workType.Name}",
                statuses), cancellationToken);
            if (workflowResult.IsFailure)
                return Failed("workflow", workType.Name, workflowResult.Error);

            entries.Add((workType.Name, new ManagedSchemeEntry(recordTypeResult.Value, workflowResult.Value)));
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning("{AppRequestName}: process {WorkProcessName} has no work type with active states, so it has no scheme.", AppRequestName, process.Name);
            return Result.Success();
        }

        // A scheme needs a default, which a process does not have: every synced work item has a type with an
        // entry. The first active type by name is used so the choice is stable from one sync to the next.
        var activeNames = process.WorkTypes.Where(t => t.IsActive).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defaultEntry = entries.FirstOrDefault(e => activeNames.Contains(e.WorkTypeName)).Entry ?? entries[0].Entry;

        var schemeKey = ManagedWorkflowKeys.Scheme(process.Id);
        var schemeResult = await _dispatcher.Send(new SyncManagedWorkflowSchemeCommand(
            ownerType, schemeKey, process.Name, defaultEntry.WorkflowId, [.. entries.Select(e => e.Entry)]), cancellationToken);
        if (schemeResult.IsFailure)
            return Failed("scheme", process.Name, schemeResult.Error);

        var workspaceIds = await _workDbContext.Workspaces
            .Where(w => w.WorkProcessId == workProcessId.Value)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        foreach (var workspaceId in workspaceIds)
        {
            var assignmentResult = await _dispatcher.Send(
                new SyncManagedWorkflowAssignmentCommand(ownerType, workspaceId, schemeKey), cancellationToken);
            if (assignmentResult.IsFailure)
                return Failed("workspace assignment", workspaceId.ToString(), assignmentResult.Error);
        }

        _logger.LogInformation("{AppRequestName}: synced {WorkTypeCount} workflows and {WorkspaceCount} workspace assignments for process {WorkProcessName}.",
            AppRequestName, entries.Count, workspaceIds.Count, process.Name);

        return Result.Success();
    }

    /// <summary>
    /// The type's states in the source's order, hidden ones included: a hidden state is kept as a retired
    /// status, because records and history still hold it.
    /// </summary>
    private static List<SourceStatus> SourceStatuses(IExternalWorkTypeWorkflow workType) =>
        [.. workType.WorkflowStates
            .OrderBy(s => s.Order)
            .Select(s => new SourceStatus(ManagedWorkflowKeys.Status(s.ExternalKey), s.StatusName.Trim(), s.Category, IsHidden: !s.IsActive))];

    private Result Failed(string what, string name, string error)
    {
        _logger.LogError("{AppRequestName}: unable to sync the {What} for {Name}. Error: {Error}", AppRequestName, what, name, error);
        return Result.Failure($"Unable to sync the {what} for {name}: {error}");
    }
}
