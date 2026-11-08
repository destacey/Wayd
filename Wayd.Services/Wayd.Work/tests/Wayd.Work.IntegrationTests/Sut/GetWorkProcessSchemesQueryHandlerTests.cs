using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkProcesses;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// The work process schemes query against SQL Server: each work type's workflow is read from the engine
/// scheme through its record type, and a workflow's statuses through a navigation backed by a field, which
/// in-memory fakes load whatever the query asks for.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetWorkProcessSchemesQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsEachWorkTypesEngineWorkflow_WithItsStatuses()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        await WorkItemHistorySeeder.EnsureWorkType(_fixture, ct);
        WorkWorkflowOwners.Register();
        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        var now = SqlServerDbContextFixture.FixedNow;
        var processId = Guid.CreateVersion7();
        var externalId = Guid.NewGuid();

        StatusWorkflow workflow;
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Work].[WorkProcesses]
                    ([Id], [Name], [Ownership], [ExternalId], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ({processId}, 'Schemes Process', 'Managed', {externalId}, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
                """,
                ct);

            var recordType = await context.RecordTypes.FirstOrDefaultAsync(r => r.OwnerType == ownerType && r.ExternalKey == WorkItemHistorySeeder.WorkTypeName, ct)
                ?? context.RecordTypes.Add(RecordType.CreateManaged(ownerType, WorkItemHistorySeeder.WorkTypeName, WorkItemHistorySeeder.WorkTypeName, EventActor.System, now).Value).Entity;
            var key = Guid.NewGuid().ToString("N");
            workflow = StatusWorkflow.CreateManaged($"Schemes {key}", ownerType, $"schemes:{key}",
                [
                    new SourceStatus("new", "New", StatusCategory.Proposed),
                    new SourceStatus("resolved", "Resolved", StatusCategory.Done, IsHidden: true),
                    new SourceStatus("closed", "Closed", StatusCategory.Done),
                ], EventActor.System, now).Value;
            context.StatusWorkflows.Add(workflow);
            context.StatusWorkflowSchemes.Add(StatusWorkflowScheme.CreateManaged(
                ownerType, "Schemes Process", ManagedWorkflowKeys.Scheme(externalId), workflow, EventActor.System, now, [(recordType, workflow)]).Value);

            var workType = await context.WorkTypes.SingleAsync(t => t.Name == WorkItemHistorySeeder.WorkTypeName, ct);
            workType.LinkRecordType(recordType.Id);
            await context.SaveChangesAsync(ct);
        }

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetWorkProcessSchemesQueryHandler(accessor.Context, accessor.Context);

        // Act
        var result = await handler.Handle(new GetWorkProcessSchemesQuery(processId), ct);

        // Assert
        var scheme = result.Should().ContainSingle().Subject;
        scheme.WorkType.Name.Should().Be(WorkItemHistorySeeder.WorkTypeName);
        scheme.WorkType.Level.Name.Should().Be("History Level");
        scheme.Workflow.Should().NotBeNull();
        (scheme.Workflow!.Id, scheme.Workflow.Key, scheme.Workflow.Name).Should().Be((workflow.Id, workflow.Key, workflow.Name));
        scheme.Workflow.Statuses.Select(s => (s.Id, s.Name, s.Category.Id, s.IsRetired)).Should().Equal(
            (workflow.Statuses.Single(s => s.Name == "New").Id, "New", (int)StatusCategory.Proposed, false),
            (workflow.Statuses.Single(s => s.Name == "Closed").Id, "Closed", (int)StatusCategory.Done, false),
            (workflow.Statuses.Single(s => s.Name == "Resolved").Id, "Resolved", (int)StatusCategory.Done, true));
    }
}
