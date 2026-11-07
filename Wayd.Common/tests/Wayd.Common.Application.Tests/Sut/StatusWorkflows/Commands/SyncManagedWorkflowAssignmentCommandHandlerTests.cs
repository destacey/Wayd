using FluentAssertions;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>The sync pointing a scope at the managed scheme its source uses.</summary>
public sealed class SyncManagedWorkflowAssignmentCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly WorkflowOwnerDescriptor Synced = new(
        "test.sync-assignment.item", "Item", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    private static readonly SourceStatus New = new("new", "New", StatusCategory.Proposed);

    public SyncManagedWorkflowAssignmentCommandHandlerTests() => WorkflowOwners.Register(Synced);

    private SyncManagedWorkflowAssignmentCommandHandler CreateSut() =>
        new(DbContext, DateTimeProvider.Object, Logger<SyncManagedWorkflowAssignmentCommandHandler>());

    private StatusWorkflowScheme SeedManagedScheme(string key)
    {
        var workflow = StatusWorkflow.CreateManaged(key, Synced.Key, key, [New], EventActor.Sync(null), Now).Value;
        DbContext.AddStatusWorkflow(workflow);
        var scheme = StatusWorkflowScheme.CreateManaged(Synced.Key, key, key, workflow, EventActor.Sync(null), Now).Value;
        DbContext.AddStatusWorkflowScheme(scheme);

        return scheme;
    }

    [Fact]
    public async Task Handle_ShouldAssignTheSchemeToTheScope_TheFirstTime()
    {
        // Arrange
        var scheme = SeedManagedScheme("azure-devops:agile");
        var scopeId = Guid.NewGuid();

        // Act
        var result = await CreateSut().Handle(new(Synced.Key, scopeId, "azure-devops:agile"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var assignment = DbContext.WorkflowAssignments.Single(a => a.Id == result.Value);
        (assignment.OwnerType, assignment.ScopeId, assignment.SchemeId).Should().Be((Synced.Key, scopeId, scheme.Id));
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheScope_WhenItsSourceUsesAnotherScheme()
    {
        // Arrange
        var agile = SeedManagedScheme("azure-devops:agile");
        var scrum = SeedManagedScheme("azure-devops:scrum");
        var scopeId = Guid.NewGuid();
        var assignment = WorkflowAssignment.Create(
            Synced.Key, scopeId, agile, DbContext.StatusWorkflows.Single(w => w.Id == agile.DefaultWorkflowId), EventActor.Sync(null), Now).Value;
        DbContext.AddWorkflowAssignment(assignment);

        // Act
        var result = await CreateSut().Handle(new(Synced.Key, scopeId, "azure-devops:scrum"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(assignment.Id);
        DbContext.WorkflowAssignments.Should().ContainSingle();
        assignment.SchemeId.Should().Be(scrum.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheSchemeDoesNotExist()
    {
        // Act
        var result = await CreateSut().Handle(new(Synced.Key, Guid.NewGuid(), "azure-devops:missing"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Workflow scheme not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
