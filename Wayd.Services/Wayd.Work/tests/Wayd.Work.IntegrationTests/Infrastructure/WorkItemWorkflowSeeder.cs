using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Work.Domain;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds what the sync keeps in the status workflow engine for one work type: its record type, a managed
/// workflow with the given statuses, a managed scheme, and that scheme's assignment to each workspace.
/// </summary>
/// <remarks>
/// Each call makes a new workflow and scheme under fresh keys, so tests never share one. The record type is
/// shared by name, as the sync keys it. Engine rows outlive the reset, which deletes the workspaces they are
/// assigned to; with fresh keys and fresh workspaces, no later test can pick them up.
/// </remarks>
public static class WorkItemWorkflowSeeder
{
    /// <summary>Seeds the work type's workflow for the workspaces and links the work type to its record type.</summary>
    /// <returns>The workflow, with its statuses.</returns>
    public static async Task<StatusWorkflow> Seed(
        SqlServerDbContextFixture fixture,
        string workTypeName,
        IReadOnlyCollection<Guid> workspaceIds,
        IReadOnlyList<SourceStatus> statuses,
        CancellationToken ct)
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

        var workType = await context.WorkTypes.SingleAsync(t => t.Name == workTypeName, ct);
        workType.LinkRecordType(recordType.Id);

        await context.SaveChangesAsync(ct);

        return workflow;
    }

    /// <summary>The seeded status named <paramref name="name"/>.</summary>
    public static WorkflowStatus Status(this StatusWorkflow workflow, string name) =>
        workflow.Statuses.Single(s => s.Name == name);
}
