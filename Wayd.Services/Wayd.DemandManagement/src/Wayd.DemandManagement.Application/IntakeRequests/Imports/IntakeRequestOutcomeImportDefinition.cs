using System.Globalization;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Imports;

/// <summary>
/// Imports what intake requests already in Wayd became: a project started for each, scope an existing project
/// absorbed (with the deliverables that meet it), or a work item.
/// </summary>
/// <remarks>
/// Run after the request import, against requests in any status: an import records history, so a request
/// imported already fulfilled takes the outcomes it had (<see cref="IntakeRequest.ImportOutcome"/>). Projects
/// and their deliverables are checked against Demand Management's copies, so a project imported moments ago may
/// not be found until its copy is made; work items are checked with Work.
/// <para>
/// Per row: each row is one outcome, and an outcome depends on nothing else in the file.
/// </para>
/// <para>
/// Gated on administering Demand Management, as the request import is: the only role spanning every desk.
/// </para>
/// </remarks>
public sealed class IntakeRequestOutcomeImportDefinition(
    IDemandManagementDbContext dbContext,
    IDispatcher dispatcher,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IFeatureFlags featureFlags,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportIntakeRequestOutcomeDto>(serializer)
{
    public const string ImportKey = "demand-management.intake-request-outcomes";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    public override string Key => ImportKey;
    public override string DisplayName => "Intake Request Outcomes";
    public override string PermissionAction => ApplicationAction.Administer;
    public override string PermissionResource => ApplicationResource.IntakeRequests;

    public override int MaxRows => 10_000;

    /// <inheritdoc />
    public override Task<bool> IsEnabled(CancellationToken cancellationToken) =>
        _featureFlags.IsEnabled(FeatureFlags.Names.DemandManagement, cancellationToken);

    protected override IReadOnlyList<ImportPass<ImportIntakeRequestOutcomeDto>> Steps =>
    [
        new("RecordIntakeRequestOutcomes", ImportPassScope.Chunked, RecordOutcomes),
    ];

    /// <inheritdoc />
    /// <remarks>Each row is recorded against its request, so the request is what it links to.</remarks>
    public override async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> DescribeCreatedRecords(
        IReadOnlyCollection<Guid> createdEntityIds,
        CancellationToken cancellationToken) =>
        await _dbContext.IntakeRequests
            .Where(x => createdEntityIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, x.Title })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ImportRecordLink(ImportRecordType.IntakeRequest, x.Key.ToString(CultureInfo.InvariantCulture), x.Title),
                cancellationToken);

    private async Task<Result> RecordOutcomes(ImportPassContext<ImportIntakeRequestOutcomeDto> context, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var userId = _currentUser.GetUserId();
        var references = await Resolve(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;

            if (!references.RequestsById.TryGetValue(data.RequestId, out var request))
            {
                row.Failed($"No intake request was found with id '{data.RequestId}'.");
                continue;
            }

            var targetId = data.TargetId;
            if (data.Kind == IntakeRequestOutcomeKind.WorkItem)
            {
                if (!references.WorkItemIds.Contains(targetId))
                {
                    row.Failed($"No work item was found with id '{targetId}'.");
                    continue;
                }
            }
            else if (!references.ProjectIds.Contains(targetId))
            {
                row.Failed($"No project was found with id '{targetId}'. A project imported moments ago may not have reached Demand Management yet.");
                continue;
            }

            var notOnProject = data.DeliverableIds
                .Where(id => !references.DeliverableProjectIds.TryGetValue(id, out var projectId) || projectId != targetId)
                .ToList();
            if (notOnProject.Count > 0)
            {
                row.Failed($"{IntakeRequestAccess.DeliverablesNotOnProjectError} {string.Join(", ", notOnProject)}.");
                continue;
            }

            var addedById = data.AddedById ?? request.AssigneeId;
            if (addedById is null)
            {
                row.Failed("AddedById is needed: the request has no assignee to have recorded the outcome.");
                continue;
            }

            if (!references.EmployeeIds.Contains(addedById.Value))
            {
                row.Failed($"No employee was found with id '{addedById}' to have recorded the outcome.");
                continue;
            }

            var recorded = request.ImportOutcome(data.Kind, targetId, data.Note, [.. data.DeliverableIds.Distinct()],
                addedById.Value, EventActor.Import(userId, addedById), data.AddedOn ?? now);
            if (recorded.IsFailure)
            {
                row.Failed(recorded.Error);
                continue;
            }

            row.Created(request.Id);
        }

        return Result.Success();
    }

    /// <summary>Loads every record the chunk's rows point at, once.</summary>
    private async Task<ResolvedReferences> Resolve(ImportPassContext<ImportIntakeRequestOutcomeDto> context, CancellationToken cancellationToken)
    {
        var rows = context.Rows.Select(r => r.Data).ToList();

        // Outcomes only, without their deliverables: recording one checks the request does not already point at
        // its target, and never changes another outcome's mapping.
        var requestIds = rows.Select(r => r.RequestId).Distinct().ToList();
        var requests = await _dbContext.IntakeRequests
            .Include(r => r.Outcomes)
            .Where(r => requestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var projectIds = rows.Where(r => r.Kind != IntakeRequestOutcomeKind.WorkItem).Select(r => r.TargetId).Distinct().ToList();
        var projects = await _dbContext.DemandProjects
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var deliverableIds = rows.SelectMany(r => r.DeliverableIds).Distinct().ToList();
        var deliverables = await _dbContext.DemandProjectDeliverables
            .Where(d => deliverableIds.Contains(d.Id))
            .Select(d => new { d.Id, d.ProjectId })
            .ToDictionaryAsync(d => d.Id, d => d.ProjectId, cancellationToken);

        var workItemIds = rows.Where(r => r.Kind == IntakeRequestOutcomeKind.WorkItem).Select(r => r.TargetId).Distinct().ToList();
        var workItems = workItemIds.Count == 0
            ? new HashSet<Guid>()
            : await _dispatcher.Send(new GetExistingWorkItemIdsQuery(workItemIds), cancellationToken);

        var employeeIds = rows.Select(r => r.AddedById)
            .Concat(requests.Values.Select(r => r.AssigneeId))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var employees = await _dbContext.Employees
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return new ResolvedReferences(requests, [.. projects], deliverables, workItems, [.. employees]);
    }

    /// <summary>The records a chunk's rows point at, by id.</summary>
    private sealed record ResolvedReferences(
        Dictionary<Guid, IntakeRequest> RequestsById,
        HashSet<Guid> ProjectIds,
        Dictionary<Guid, Guid> DeliverableProjectIds,
        IReadOnlySet<Guid> WorkItemIds,
        HashSet<Guid> EmployeeIds);
}
