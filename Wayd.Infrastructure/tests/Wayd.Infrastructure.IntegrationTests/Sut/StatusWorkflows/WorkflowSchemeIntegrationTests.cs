using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// Record types and schemes through a real save, and the resolver reading a type's workflow from them.
/// </summary>
/// <remarks>
/// The resolver picks the type's entry or the scheme's default inside one projection, which the in-memory
/// fakes evaluate as LINQ-to-Objects; only SQL Server shows it translates. The owner type is this test's own,
/// so no seeded assignment is touched.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class WorkflowSchemeIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 1, 10, 0, 0);

    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.integration.ticket", "Ticket", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static StatusWorkflow Published(string name)
    {
        var workflow = StatusWorkflow.Create(name, null, Ticket.Key, EventActor.System, Now).Value;
        workflow.AddStatus("New", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Done", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();

        return workflow;
    }

    [Fact]
    public async Task ForRecordType_ShouldResolveEachTypesWorkflow_FromTheStoredScheme()
    {
        // Arrange
        WorkflowOwners.Register(Ticket);
        var ct = TestContext.Current.CancellationToken;
        var scopeId = Guid.CreateVersion7();

        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var bug = RecordType.Create(Ticket.Key, $"Bug {scopeId}", EventActor.System, Now).Value;
        var story = RecordType.Create(Ticket.Key, $"Story {scopeId}", EventActor.System, Now).Value;
        var scheme = StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, standard, EventActor.System, Now).Value;
        scheme.SetEntry(bug, bugFlow, StatusRemap.AutoMap(standard, bugFlow).Value, EventActor.System, Now).IsSuccess.Should().BeTrue();
        var assignment = WorkflowAssignment.Create(Ticket.Key, scopeId, scheme, standard, EventActor.System, Now).Value;

        await using (var setup = _fixture.CreateContext())
        {
            setup.StatusWorkflows.AddRange(standard, bugFlow);
            setup.RecordTypes.AddRange(bug, story);
            setup.StatusWorkflowSchemes.Add(scheme);
            setup.WorkflowAssignments.Add(assignment);
            await setup.SaveChangesAsync(ct);
        }

        await using var context = _fixture.CreateContext();
        var sut = new StatusResolver(context);

        // Act
        var forBug = await sut.ForRecordType(Ticket.Key, scopeId, bug.Id, ct);
        var forStory = await sut.ForRecordType(Ticket.Key, scopeId, story.Id, ct);
        var untyped = await sut.ForScope(Ticket.Key, scopeId, ct);

        // Assert
        forBug.IsSuccess.Should().BeTrue(forBug.IsFailure ? forBug.Error : null);
        forBug.Value.Id.Should().Be(bugFlow.Id);
        forStory.Value.Id.Should().Be(standard.Id);
        untyped.Value.Id.Should().Be(standard.Id);

        var stored = await context.StatusWorkflowSchemes
            .Include(s => s.Entries)
            .SingleAsync(s => s.Id == scheme.Id, ct);
        stored.Key.Should().BePositive();
        stored.Entries.Should().ContainSingle().Which.WorkflowId.Should().Be(bugFlow.Id);
    }
}
