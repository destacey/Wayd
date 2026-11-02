using FluentAssertions;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Reading and choosing one scope's workflow, for a module whose own screens choose it.
/// </summary>
public sealed class ScopedWorkflowAssignmentsTests : StatusWorkflowHandlerTestBase
{
    private readonly Guid _scopeId = Guid.CreateVersion7();

    private ScopedWorkflowAssignments CreateSut() => new(DbContext);

    private WorkflowAssignment Assign(StatusWorkflow workflow, Guid? scopeId)
    {
        var assignment = TestWorkflowAssignments.ForWorkflow(Widget.Key, scopeId, workflow, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);
        return assignment;
    }

    [Fact]
    public async Task Current_ForAScopeWithoutItsOwn_IsTheOrganizationsDefault()
    {
        // Arrange
        var organizationDefault = SeedWorkflow("Default", publish: true);
        Assign(organizationDefault, null);

        // Act
        var current = await CreateSut().Current(Widget.Key, _scopeId, TestContext.Current.CancellationToken);

        // Assert
        current.Value.Workflow.Id.Should().Be(organizationDefault.Id);
        current.Value.IsScopeOwn.Should().BeFalse();
    }

    [Fact]
    public async Task Current_ForAScopeWithItsOwn_IsThatOne()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var own = SeedWorkflow("Own", publish: true);
        Assign(own, _scopeId);

        // Act
        var current = await CreateSut().Current(Widget.Key, _scopeId, TestContext.Current.CancellationToken);

        // Assert
        current.Value.Workflow.Id.Should().Be(own.Id);
        current.Value.IsScopeOwn.Should().BeTrue();
    }

    [Fact]
    public async Task Published_ListsOnlyPublishedWorkflowsOfTheOwnerType_ByName()
    {
        // Arrange
        SeedWorkflow("Zeta", publish: true);
        SeedWorkflow("Alpha", publish: true);
        SeedWorkflow("Draft only");
        SeedWorkflow("Elsewhere", publish: true, owner: Gadget);

        // Act
        var published = await CreateSut().Published(Widget.Key, TestContext.Current.CancellationToken);

        // Assert
        published.Select(w => w.Name).Should().Equal("Alpha", "Zeta");
    }

    [Fact]
    public async Task AssignToEmptyScope_ForAScopeFollowingTheDefault_GivesItItsOwnAssignment()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var chosen = SeedWorkflow("Chosen", publish: true);

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, chosen.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var own = DbContext.WorkflowAssignments.Single(a => a.ScopeId == _scopeId);
        own.Scheme!.DefaultWorkflowId.Should().Be(chosen.Id);
        own.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowAssignedEvent>();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task AssignToEmptyScope_ForAScopeWithItsOwn_ReassignsIt()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);
        var chosen = SeedWorkflow("Chosen", publish: true);

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, chosen.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        own.Scheme!.DefaultWorkflowId.Should().Be(chosen.Id);
        DbContext.WorkflowAssignments.Count(a => a.ScopeId == _scopeId).Should().Be(1);
    }

    [Fact]
    public async Task AssignToEmptyScope_ToTheWorkflowItAlreadyFollows_ChangesNothing()
    {
        // Arrange — choosing the default for a scope that follows it keeps it following
        var organizationDefault = SeedWorkflow("Default", publish: true);
        Assign(organizationDefault, null);

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, organizationDefault.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AssignToEmptyScope_ToTheOrganizationsWorkflow_RemovesTheScopesOwn_SoItFollowsTheDefaultAgain()
    {
        // Arrange
        var organizationDefault = SeedWorkflow("Default", publish: true);
        Assign(organizationDefault, null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, organizationDefault.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        var removed = own.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowAssignmentRemovedEvent>().Subject;
        removed.FromWorkflowId.Should().Be(own.Scheme!.DefaultWorkflowId);
        removed.ToWorkflowId.Should().Be(organizationDefault.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
        (await CreateSut().Current(Widget.Key, _scopeId, TestContext.Current.CancellationToken)).Value.IsScopeOwn.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveScope_RemovesTheScopesOwnAssignment_WithoutSaving()
    {
        // Arrange — the caller saves it with the scope's own removal
        Assign(SeedWorkflow("Default", publish: true), null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);

        // Act
        var result = await CreateSut().RemoveScope(Widget.Key, _scopeId, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        own.DomainEvents.OfType<WorkflowAssignmentRemovedEvent>().Should().ContainSingle()
            .Which.ToWorkflowId.Should().BeNull("the scope itself is gone");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveScope_ForAScopeFollowingTheDefault_ChangesNothing()
    {
        // Arrange
        var organizationDefault = Assign(SeedWorkflow("Default", publish: true), null);

        // Act
        var result = await CreateSut().RemoveScope(Widget.Key, _scopeId, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().ContainSingle().Which.Should().BeSameAs(organizationDefault);
    }

    [Fact]
    public async Task AssignToEmptyScope_ToADraftWorkflow_Fails()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var draft = SeedWorkflow("Draft");

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, draft.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be assigned.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AssignToEmptyScope_ToAWorkflowOfAnotherOwnerType_ReadsAsNotFound()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var elsewhere = SeedWorkflow("Elsewhere", publish: true, owner: Gadget);

        // Act
        var result = await CreateSut().AssignToEmptyScope(Widget.Key, _scopeId, elsewhere.Id, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
    }
}
