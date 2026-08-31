using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

/// <summary>
/// The status history every status-tracked aggregate keeps. A workflow only describes its current
/// shape, so what a record went through has to be recorded as it happens rather than reconstructed.
/// </summary>
public sealed class StatusHistoryTests
{
    private const string ProductName = "Checkout";

    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ReleaseFaker _faker;

    public StatusHistoryTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new ReleaseFaker();
    }

    private Release PlannedRelease(Guid workflowId)
    {
        var planned = StatusRefFactory.For(StatusCategory.Proposed, workflowId: workflowId, name: "Planned");

        return Release.Create(
            Guid.CreateVersion7(), "4.8.2", null, null, null, true, planned,
            ProductName, EventActor.System, _dateTimeProvider.Now).Value;
    }

    #region Recording

    [Fact]
    public void Create_ShouldRecordTheOpeningTransition()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();

        // Act
        var sut = PlannedRelease(workflowId);

        // Assert
        // The record entering its first status is itself history — without it, the earliest known state
        // would be whatever the first change happened to move away from.
        var transition = sut.StatusTransitions.Should().ContainSingle().Subject;
        transition.FromStatusId.Should().BeNull();
        transition.ToStatusName.Should().Be("Planned");
        transition.WorkflowId.Should().Be(workflowId);
        transition.Sequence.Should().Be(0);
    }

    [Fact]
    public void EachChange_ShouldAppendATransition()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);

        // Act
        sut.Cut(new LocalDate(2026, 9, 1),
            StatusRefFactory.For(StatusCategory.Active, ProductStatusAlias.Ready, workflowId, "Ready"),
            ProductName, EventActor.System, _dateTimeProvider.Now);
        sut.MarkReleased(new LocalDate(2026, 9, 5),
            StatusRefFactory.For(StatusCategory.Done, ProductStatusAlias.Released, workflowId, "Released"),
            ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.StatusTransitions.Select(t => t.ToStatusName).Should().Equal("Planned", "Ready", "Released");
        sut.StatusTransitions.Select(t => t.Sequence).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void EachTransition_ShouldChainFromThePrevious()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);

        // Act
        sut.Cut(new LocalDate(2026, 9, 1),
            StatusRefFactory.For(StatusCategory.Active, ProductStatusAlias.Ready, workflowId, "Ready"),
            ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var cut = sut.StatusTransitions.Last();
        cut.FromStatusName.Should().Be("Planned");
        cut.FromCategory.Should().Be(StatusCategory.Proposed);
        cut.ToStatusName.Should().Be("Ready");
        cut.ToCategory.Should().Be(StatusCategory.Active);
    }

    [Fact]
    public void Withdraw_ShouldRecordItsReason()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);

        // Act
        sut.Withdraw("Critical defect found in staging.",
            StatusRefFactory.For(StatusCategory.Removed, ProductStatusAlias.Withdrawn, workflowId, "Withdrawn"),
            ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.StatusTransitions.Last().Reason.Should().Be("Critical defect found in staging.");
    }

    [Fact]
    public void EachTransition_ShouldRecordWhoMadeIt()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var actor = EventActor.Import("user-42");
        var planned = StatusRefFactory.For(StatusCategory.Proposed, workflowId: workflowId, name: "Planned");

        // Act
        var sut = Release.Create(
            Guid.CreateVersion7(), "4.8.2", null, null, null, true, planned,
            ProductName, actor, _dateTimeProvider.Now).Value;

        // Assert
        // An import attributes its history to the import, not to whoever happened to start it.
        var transition = sut.StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be("user-42");
        transition.ChangedOn.Should().Be(_dateTimeProvider.Now);
    }

    #endregion Recording

    #region Immutability

    [Fact]
    public void ATransition_ShouldKeepTheNameTheStatusHadAtTheTime()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);

        var readyStatusId = Guid.CreateVersion7();
        var readyThen = new StatusRef(workflowId, readyStatusId, "Ready", StatusCategory.Active, (int)ProductStatusAlias.Ready);
        sut.Cut(new LocalDate(2026, 9, 1), readyThen, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Act
        // The administrator renames the status; the same status id now reads differently.
        var readyNow = new StatusRef(workflowId, readyStatusId, "Cut", StatusCategory.Active, (int)ProductStatusAlias.Ready);

        // Assert
        // The history still says what it said. Renaming a live status must not rewrite the past — the
        // reason both the id and the name are frozen on every row.
        var recorded = sut.StatusTransitions.Last();
        recorded.ToStatusId.Should().Be(readyNow.StatusId);
        recorded.ToStatusName.Should().Be("Ready");
    }

    [Fact]
    public void ATransition_ShouldKeepTheAliasSoAMetricStaysStable()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);

        // Act
        sut.Withdraw(null,
            StatusRefFactory.For(StatusCategory.Removed, ProductStatusAlias.Withdrawn, workflowId, "Withdrawn"),
            ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Withdrawal rate is computed over history, so the alias has to survive a later restructuring
        // of the workflow.
        sut.StatusTransitions.Last().ToAlias.Should().Be((int)ProductStatusAlias.Withdrawn);
    }

    #endregion Immutability

    #region Which workflow

    [Fact]
    public void EachTransition_ShouldSayWhichWorkflowGovernedIt()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();

        // Act
        var sut = PlannedRelease(workflowId);

        // Assert
        // The record itself does not name a workflow — that is its container's, through the assignment.
        // Which workflow governed a past change is frozen here, so reassigning the container later
        // cannot rewrite it.
        sut.StatusTransitions.Last().WorkflowId.Should().Be(workflowId);
    }

    [Fact]
    public void EveryAggregate_ShouldDeclareItsOwnerType()
    {
        // Arrange
        var release = _faker.Generate();

        // Act & Assert
        // The owner type is what lets one transitions table serve every module.
        release.StatusOwnerType.Should().Be(ProductWorkflowOwners.Release.Key);
    }

    #endregion Which workflow

    #region No-ops

    [Fact]
    public void MovingToTheSameStatus_ShouldNotAppendATransition()
    {
        // Arrange
        var workflowId = Guid.CreateVersion7();
        var sut = PlannedRelease(workflowId);
        var current = new StatusRef(workflowId, sut.StatusId, sut.StatusName, sut.StatusCategory);

        // Act
        sut.MoveTargetDate(new LocalDate(2026, 10, 1), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A change that does not move status leaves the history alone.
        sut.StatusTransitions.Should().ContainSingle();
        current.StatusId.Should().Be(sut.StatusId);
    }

    #endregion No-ops
}
