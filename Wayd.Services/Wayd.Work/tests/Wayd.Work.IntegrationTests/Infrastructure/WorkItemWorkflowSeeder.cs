using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Application.WorkProcesses;
using Wayd.Work.Domain;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds what the sync keeps in the status workflow engine for one work type: its record type, a managed
/// workflow with the given statuses, a managed scheme, and that scheme's assignment to each workspace.
/// </summary>
/// <remarks>
/// Each call makes a new workflow and scheme under fresh keys, so tests never share one. Given a workspace whose
/// process lists the work type, it also gives that process its own managed scheme, which is where the sync reads a
/// process's work types from; the workspaces stay assigned to the fresh scheme. The record type is
/// shared by name, as the sync keys it. Engine rows outlive the reset, which deletes the workspaces they are
/// assigned to; with fresh keys and fresh workspaces, no later test can pick them up.
/// </remarks>
public static class WorkItemWorkflowSeeder
{
    /// <summary>Seeds the work type's workflow for the workspaces and links the work type to its record type.</summary>
    /// <param name="processOf">
    /// A workspace whose process lists the work type: the process is given an external id if it has none, and its
    /// own managed scheme with an entry for the type, unless it already has one with that entry. A scheme without
    /// it is refused rather than left missing the type.
    /// </param>
    /// <returns>The workflow, with its statuses.</returns>
    public static async Task<StatusWorkflow> Seed(
        SqlServerDbContextFixture fixture,
        string workTypeName,
        IReadOnlyCollection<Guid> workspaceIds,
        IReadOnlyList<SourceStatus> statuses,
        CancellationToken ct,
        Guid? processOf = null)
    {
        WorkWorkflowOwners.Register();
        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var accessor = new WaydDbContextAccessor(fixture);
        var context = accessor.Context;

        var recordType = await context.RecordTypes.FirstOrDefaultAsync(r => r.OwnerType == ownerType && r.ExternalKey == workTypeName, ct);
        if (recordType is null)
        {
            recordType = RecordType.CreateManaged(ownerType, workTypeName, workTypeName, EventActor.System, now).Value;
            context.RecordTypes.Add(recordType);
        }

        var key = Guid.NewGuid().ToString("N");
        var workflowResult = StatusWorkflow.CreateManaged($"Seeded {key}", ownerType, $"seeded:{key}", statuses, EventActor.System, now);
        workflowResult.IsSuccess.Should().BeTrue(workflowResult.IsFailure ? workflowResult.Error : null);
        var workflow = workflowResult.Value;
        context.StatusWorkflows.Add(workflow);

        var scheme = StatusWorkflowScheme.CreateManaged(ownerType, $"Seeded {key}", $"seeded:{key}", workflow, EventActor.System, now,
            [(recordType, workflow)]).Value;
        context.StatusWorkflowSchemes.Add(scheme);

        foreach (var workspaceId in workspaceIds)
        {
            context.WorkflowAssignments.Add(WorkflowAssignment.Create(ownerType, workspaceId, scheme, workflow, EventActor.System, now).Value);
        }

        if (processOf is Guid processWorkspaceId)
        {
            var processSchemeKey = ManagedWorkflowKeys.Scheme(await ProcessExternalId(context, processWorkspaceId, ct));
            var processScheme = await context.StatusWorkflowSchemes.Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.OwnerType == ownerType && s.ExternalKey == processSchemeKey, ct);
            if (processScheme is null)
            {
                context.StatusWorkflowSchemes.Add(StatusWorkflowScheme.CreateManaged(ownerType, $"Process {key}", processSchemeKey, workflow,
                    EventActor.System, now, [(recordType, workflow)]).Value);
            }
            else if (!processScheme.Entries.Any(e => e.RecordTypeId == recordType.Id))
            {
                throw new InvalidOperationException($"The process of workspace {processWorkspaceId} already has a scheme without {workTypeName}; seed every type of a process through one scheme.");
            }
        }

        var workType = await context.WorkTypes.SingleAsync(t => t.Name == workTypeName, ct);
        workType.LinkRecordType(recordType.Id);

        await context.SaveChangesAsync(ct);

        return workflow;
    }

    /// <summary>The external id of the workspace's process, given one first if it has none.</summary>
    private static async Task<Guid> ProcessExternalId(WaydDbContext context, Guid workspaceId, CancellationToken ct)
    {
        var processId = await context.Workspaces.IgnoreQueryFilters().Where(w => w.Id == workspaceId).Select(w => w.WorkProcessId).SingleAsync(ct);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Work].[WorkProcesses] SET [ExternalId] = {Guid.NewGuid()} WHERE [Id] = {processId} AND [ExternalId] IS NULL;", ct);
        return await context.WorkProcesses.IgnoreQueryFilters().Where(p => p.Id == processId).Select(p => p.ExternalId!.Value).SingleAsync(ct);
    }

    /// <summary>The seeded status named <paramref name="name"/>.</summary>
    public static WorkflowStatus Status(this StatusWorkflow workflow, string name) =>
        workflow.Statuses.Single(s => s.Name == name);
}
