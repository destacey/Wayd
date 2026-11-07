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
/// use by another process. A work type whose states are all hidden keeps its workflow once it has one, with
/// every status retired, so its records and history still resolve; one with no workflow yet gets none, and
/// stays out of the scheme until a state is offered. A type the process no longer lists keeps its scheme
/// entry for the same reason.
/// </para>
/// </remarks>
public sealed class SyncExternalWorkProcessWorkflowsCommandHandler(
    IWorkDbContext workDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    IDispatcher dispatcher,
    ILogger<SyncExternalWorkProcessWorkflowsCommandHandler> logger)
    : ICommandHandler<SyncExternalWorkProcessWorkflowsCommand>
{
    private const string AppRequestName = nameof(SyncExternalWorkProcessWorkflowsCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
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

        var workTypeNames = await WorkTypeNames(cancellationToken);
        var workflowKeys = await ExistingWorkflowKeys(ownerType, process.Id, cancellationToken);

        var entries = new List<SyncedType>();
        foreach (var workType in process.WorkTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var statuses = SourceStatuses(workType);
            var workflowKey = ManagedWorkflowKeys.Workflow(process.Id, workType.ExternalKey);
            if (statuses.Count == 0 || (statuses.All(s => s.IsHidden) && !workflowKeys.Contains(workflowKey)))
            {
                _logger.LogDebug("{AppRequestName}: work type {WorkType} in process {WorkProcessName} offers no state, so it has no workflow.",
                    AppRequestName, workType.Name, process.Name);
                continue;
            }

            // Work's own name for the type, which every process and source shares: two processes that spell it
            // differently must not rename the one record type back and forth.
            var recordTypeName = workTypeNames.GetValueOrDefault(workType.Name.Trim(), workType.Name.Trim());
            var recordTypeResult = await _dispatcher.Send(new SyncManagedRecordTypeCommand(
                ownerType, ManagedWorkflowKeys.RecordType(recordTypeName), recordTypeName), cancellationToken);
            if (recordTypeResult.IsFailure)
                return Failed("record type", workType.Name, recordTypeResult.Error);

            var workflowResult = await _dispatcher.Send(new SyncManagedWorkflowCommand(
                ownerType, workflowKey, $"{process.Name.Trim()} - {workType.Name.Trim()}", statuses), cancellationToken);
            if (workflowResult.IsFailure)
                return Failed("workflow", workType.Name, workflowResult.Error);

            entries.Add(new SyncedType(
                new ManagedSchemeEntry(recordTypeResult.Value, workflowResult.Value),
                IsActive: workType.IsActive,
                OffersAState: statuses.Any(s => !s.IsHidden)));
        }

        if (entries.Count == 0)
        {
            _logger.LogDebug("{AppRequestName}: process {WorkProcessName} has no work type that offers a state, so it has no scheme.", AppRequestName, process.Name);
            return Result.Success();
        }

        // A scheme needs a default, which a process does not have: every synced work item has a type with an
        // entry. The first type by name that is active and offers a state is used, so the choice is stable from
        // one sync to the next and never a workflow records cannot be given a status in.
        var defaultEntry = (entries.FirstOrDefault(e => e.IsActive && e.OffersAState)
            ?? entries.FirstOrDefault(e => e.OffersAState)
            ?? entries[0]).Entry;

        var schemeKey = ManagedWorkflowKeys.Scheme(process.Id);
        var schemeResult = await _dispatcher.Send(new SyncManagedWorkflowSchemeCommand(
            ownerType, schemeKey, process.Name.Trim(), defaultEntry.WorkflowId, [.. entries.Select(e => e.Entry)]), cancellationToken);
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

    /// <summary>Work's work type names, by any casing of them.</summary>
    private async Task<Dictionary<string, string>> WorkTypeNames(CancellationToken cancellationToken)
    {
        var names = await _workDbContext.WorkTypes.Select(t => t.Name).ToListAsync(cancellationToken);
        return names.DistinctBy(n => n, StringComparer.OrdinalIgnoreCase).ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The source keys of the process's workflows the engine already keeps.</summary>
    private async Task<HashSet<string>> ExistingWorkflowKeys(string ownerType, Guid processExternalId, CancellationToken cancellationToken)
    {
        var prefix = ManagedWorkflowKeys.WorkflowPrefix(processExternalId);
        var keys = await _statusWorkflowDbContext.StatusWorkflows
            .Where(w => w.OwnerType == ownerType && w.ExternalKey != null && w.ExternalKey.StartsWith(prefix))
            .Select(w => w.ExternalKey!)
            .ToListAsync(cancellationToken);
        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The type's states in the source's order, hidden ones included: a hidden state is kept as a retired
    /// status, because records and history still hold it.
    /// </summary>
    private static List<SourceStatus> SourceStatuses(IExternalWorkTypeWorkflow workType) =>
        [.. workType.WorkflowStates
            .OrderBy(s => s.Order)
            .Select(s => new SourceStatus(ManagedWorkflowKeys.Status(s.ExternalKey), s.StatusName.Trim(), s.Category, IsHidden: !s.IsActive))];

    /// <summary>Not logged here: the caller logs the failure once, with what it decides to do about it.</summary>
    private static Result Failed(string what, string name, string error) =>
        Result.Failure($"Unable to sync the {what} for {name}: {error}");

    /// <summary>One work type as synced: its scheme entry, and what decides whether it can be the default.</summary>
    private sealed record SyncedType(ManagedSchemeEntry Entry, bool IsActive, bool OffersAState);
}
