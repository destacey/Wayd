using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Tests.Shared.Data;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows.Enums;
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
    private readonly Mock<IStatusRecordMigrator> _migrator = new();
    private readonly Mock<IStatusRecordCounter> _counter = new();

    public ScopedWorkflowAssignmentsTests()
    {
        _migrator.SetupGet(m => m.OwnerType).Returns(Widget.Key);
        _migrator.Setup(m => m.Migrate(It.IsAny<StatusRemap>(), It.IsAny<Guid?>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(0));
        _counter.SetupGet(c => c.OwnerType).Returns(Widget.Key);
        HoldingRecords();
        LeftOn();
    }

    /// <summary>The workflows besides the scope's own that the counter reports its records on; none by default.</summary>
    private void LeftOn(params Guid[] workflowIds) =>
        _counter.Setup(c => c.WorkflowsBesides(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<Guid>>(workflowIds));

    /// <summary>The scope's records the counter reports, keyed by status id; none by default.</summary>
    private void HoldingRecords(params (Guid StatusId, int Count)[] counts) =>
        _counter.Setup(c => c.CountByStatus(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyDictionary<Guid, int>>(counts.ToDictionary(c => c.StatusId, c => c.Count)));

    /// <summary>A published workflow sharing only Notable and Terminal with <see cref="StatusWorkflowHandlerTestBase.SeedWorkflow"/>'s,
    /// so its Proposed status has nowhere automatic to go.</summary>
    private StatusWorkflow SeedWorkflowWithoutProposed(string name)
    {
        var workflow = StatusWorkflow.Create(name, null, Widget.Key, EventActor.System, Now).Value;
        workflow.AddStatus("Draft", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Queued", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Now);
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Now);
        workflow.Publish(EventActor.System, Now);
        workflow.ClearDomainEvents();
        DbContext.AddStatusWorkflow(workflow);
        return workflow;
    }

    private ScopedWorkflowAssignments CreateSut() => new(DbContext, [_migrator.Object], [_counter.Object]);

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
    public async Task Change_ForAScopeFollowingTheDefault_GivesItItsOwnAssignment()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var chosen = SeedWorkflow("Chosen", publish: true);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var own = DbContext.WorkflowAssignments.Single(a => a.ScopeId == _scopeId);
        own.Scheme!.DefaultWorkflowId.Should().Be(chosen.Id);
        own.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowAssignedEvent>();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Change_ForAScopeWithItsOwn_ReassignsIt()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);
        var chosen = SeedWorkflow("Chosen", publish: true);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        own.Scheme!.DefaultWorkflowId.Should().Be(chosen.Id);
        DbContext.WorkflowAssignments.Count(a => a.ScopeId == _scopeId).Should().Be(1);
    }

    [Fact]
    public async Task Change_ToTheWorkflowItAlreadyFollows_ChangesNothing()
    {
        // Arrange — choosing the default for a scope that follows it keeps it following
        var organizationDefault = SeedWorkflow("Default", publish: true);
        Assign(organizationDefault, null);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, organizationDefault.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        _migrator.Verify(m => m.Migrate(It.IsAny<StatusRemap>(), It.IsAny<Guid?>(), It.IsAny<EventActor>(), It.IsAny<Instant>(),
            It.IsAny<CancellationToken>()), Times.Never());
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Change_ToTheWorkflowItAlreadyFollows_MovesRecordsLeftOnAnother_AndKeepsTheAssignment()
    {
        // Arrange — a record created while an earlier change ran landed on the workflow the scope left
        var left = SeedWorkflow("Left", publish: true);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);
        Assign(SeedWorkflow("Default", publish: true), null);
        var followed = own.Scheme!.DefaultWorkflowId;
        LeftOn(left.Id);
        _migrator.Setup(m => m.Migrate(It.IsAny<StatusRemap>(), _scopeId, It.IsAny<EventActor>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(1));

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, followed, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(1);
        _migrator.Verify(m => m.Migrate(It.Is<StatusRemap>(r => r.FromWorkflowId == left.Id && r.ToWorkflowId == followed),
            _scopeId, It.IsAny<EventActor>(), Now, It.IsAny<CancellationToken>()), Times.Once());
        own.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Change_ToTheWorkflowItAlreadyFollows_WhenARecordLeftOnAnotherCannotBePlaced_FailsAndSavesNothing()
    {
        // Arrange
        var left = SeedWorkflow("Left", publish: true);
        var followed = SeedWorkflowWithoutProposed("Followed");
        Assign(followed, _scopeId);
        LeftOn(left.Id);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, followed.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Proposed");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Change_ToTheOrganizationsWorkflow_RemovesTheScopesOwn_SoItFollowsTheDefaultAgain()
    {
        // Arrange
        var organizationDefault = SeedWorkflow("Default", publish: true);
        Assign(organizationDefault, null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, organizationDefault.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

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
    public async Task Change_ForAScopeHoldingRecords_MovesThemWithTheDecisionsBeforeSaving()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflowWithoutProposed("Chosen");
        HoldingRecords((current.Status("Proposed").Id, 2));
        _migrator.Setup(m => m.Migrate(It.IsAny<StatusRemap>(), _scopeId, It.IsAny<EventActor>(), Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(2));
        StatusRemapDecisionDto[] decisions = [new() { FromStatusId = current.Status("Proposed").Id, ToStatusId = chosen.Status("Queued").Id }];

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, decisions, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(2);
        _migrator.Verify(m => m.Migrate(
            It.Is<StatusRemap>(r => r.FromWorkflowId == current.Id && r.ToWorkflowId == chosen.Id
                && r.For(current.Status("Proposed").Id)!.StatusId == chosen.Status("Queued").Id),
            _scopeId, It.IsAny<EventActor>(), Now, It.IsAny<CancellationToken>()), Times.Once());
        DbContext.WorkflowAssignments.Single(a => a.ScopeId == _scopeId).Scheme!.DefaultWorkflowId.Should().Be(chosen.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Change_ForAScopeHoldingRecords_WithAStatusLeftUnplaced_FailsAndMovesNothing()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflowWithoutProposed("Chosen");
        HoldingRecords((current.Status("Notable").Id, 1));

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Proposed");
        _migrator.Verify(m => m.Migrate(It.IsAny<StatusRemap>(), It.IsAny<Guid?>(), It.IsAny<EventActor>(), It.IsAny<Instant>(),
            It.IsAny<CancellationToken>()), Times.Never());
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Change_ForAnEmptyScope_NeedsNoDecisions_AndStillAsksTheMigrator()
    {
        // Arrange — a record arriving since the count is moved rather than left behind
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflowWithoutProposed("Chosen");

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _migrator.Verify(m => m.Migrate(It.Is<StatusRemap>(r => r.IsComplete), _scopeId, It.IsAny<EventActor>(), Now,
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Change_WhenTheMigratorRefuses_SavesNothing()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflow("Chosen", publish: true);
        HoldingRecords((current.Status("Notable").Id, 1));
        _migrator.Setup(m => m.Migrate(It.IsAny<StatusRemap>(), It.IsAny<Guid?>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int>("A record cannot land there."));

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A record cannot land there.");
        DbContext.WorkflowAssignments.Should().NotContain(a => a.ScopeId == _scopeId);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Change_ForAScopeHoldingRecords_WithNoMigrator_Fails()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflow("Chosen", publish: true);
        HoldingRecords((current.Status("Notable").Id, 1));
        var sut = new ScopedWorkflowAssignments(DbContext, [], [_counter.Object]);

        // Act
        var result = await sut.Change(Widget.Key, _scopeId, chosen.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PreviewChange_MapsTheScopesWorkflowAndCountsItsRecords()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);
        var chosen = SeedWorkflowWithoutProposed("Chosen");
        HoldingRecords((current.Status("Proposed").Id, 3), (current.Status("Notable").Id, 1));

        // Act
        var result = await CreateSut().PreviewChange(Widget.Key, _scopeId, chosen.Id, TestContext.Current.CancellationToken);

        // Assert
        var preview = result.Value;
        preview.Remap.From.Id.Should().Be(current.Id);
        preview.Remap.AffectedRecordCount.Should().Be(4);
        preview.Remap.IsComplete.Should().BeFalse();
        preview.Remap.Entries.Single(e => e.From.Name == "Proposed").Should().Match<StatusRemapEntryDto>(e => e.To == null && e.RecordCount == 3);
        preview.TargetStatuses.Select(s => s.Name).Should().Equal("Draft", "Queued", "Notable", "Terminal");
        _counter.Verify(c => c.CountByStatus(current.Id, _scopeId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PreviewChange_ToTheWorkflowTheScopeFollows_Fails()
    {
        // Arrange
        var current = SeedWorkflow("Default", publish: true);
        Assign(current, null);

        // Act
        var result = await CreateSut().PreviewChange(Widget.Key, _scopeId, current.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScopedWorkflowAssignments.AlreadyFollowsError);
    }

    [Fact]
    public async Task PreviewChange_ToADraftWorkflow_Fails()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var draft = SeedWorkflow("Draft");

        // Act
        var result = await CreateSut().PreviewChange(Widget.Key, _scopeId, draft.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScopedWorkflowAssignments.NotPublishedError);
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
    public async Task Unassign_RecordsTheOrganizationsSchemeTheScopeFollowsNow_WithoutSaving()
    {
        // Arrange
        var organizationDefault = SeedWorkflow("Default", publish: true);
        var organization = Assign(organizationDefault, null);
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);

        // Act
        var result = await CreateSut().Unassign(Widget.Key, _scopeId, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        // The scope stays, so the event names where it lands rather than reading as the scope being removed.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().ContainSingle().Which.Should().BeSameAs(organization);
        var removed = own.DomainEvents.OfType<WorkflowAssignmentRemovedEvent>().Should().ContainSingle().Subject;
        removed.ToWorkflowId.Should().Be(organizationDefault.Id);
        removed.ToSchemeId.Should().Be(organization.SchemeId);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Unassign_LeavesTheScopeFollowingNothing_WhenTheOrganizationHasNoAssignment()
    {
        // Arrange
        var own = Assign(SeedWorkflow("Own", publish: true), _scopeId);

        // Act
        var result = await CreateSut().Unassign(Widget.Key, _scopeId, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeTrue();
        DbContext.WorkflowAssignments.Should().BeEmpty();
        var removed = own.DomainEvents.OfType<WorkflowAssignmentRemovedEvent>().Should().ContainSingle().Subject;
        (removed.ToWorkflowId, removed.ToSchemeId).Should().Be((null, null));
    }

    [Fact]
    public async Task Unassign_ReportsNothingRemoved_ForAScopeWithoutItsOwn()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);

        // Act
        var result = await CreateSut().Unassign(Widget.Key, _scopeId, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        DbContext.WorkflowAssignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Change_ToADraftWorkflow_Fails()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var draft = SeedWorkflow("Draft");

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, draft.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be assigned.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Change_ToAWorkflowOfAnotherOwnerType_ReadsAsNotFound()
    {
        // Arrange
        Assign(SeedWorkflow("Default", publish: true), null);
        var elsewhere = SeedWorkflow("Elsewhere", publish: true, owner: Gadget);

        // Act
        var result = await CreateSut().Change(Widget.Key, _scopeId, elsewhere.Id, [], EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Status workflow not found.");
    }
}

/// <summary>Looks a seeded workflow's status up by name.</summary>
file static class WorkflowStatusLookup
{
    public static WorkflowStatus Status(this StatusWorkflow workflow, string name) => workflow.Statuses.Single(s => s.Name == name);
}
