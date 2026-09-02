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

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

/// <summary>
/// Moving a record onto a different workflow. Statuses are never shared between workflows, so every
/// record in a scope has to be translated when its container is reassigned.
/// </summary>
public sealed class SwitchWorkflowTests
{
    private const string ProductName = "Checkout";

    private readonly TestingDateTimeProvider _dateTimeProvider;

    public SwitchWorkflowTests()
    {
        ProductWorkflowOwners.Register();
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    private static StatusWorkflow VersionWorkflow(string name)
    {
        var workflow = StatusWorkflow.Create(name, null, ProductWorkflowOwners.Version.Key).Value;
        workflow.AddStatus("Planned", null, StatusCategory.Proposed);
        workflow.AddStatus("Ready", null, StatusCategory.Active, (int)ProductStatusAlias.Ready);
        workflow.AddStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        workflow.AddStatus("Withdrawn", null, StatusCategory.Removed, (int)ProductStatusAlias.Withdrawn);

        return workflow;
    }

    private Version VersionOn(StatusWorkflow workflow)
    {
        var planned = StatusRef.From(workflow.Statuses.Single(s => s.Name == "Planned"));

        return Version.Create(
            Guid.CreateVersion7(), "4.8.2", null, null, null, true, planned,
            ProductName, EventActor.System, _dateTimeProvider.Now).Value;
    }

    #region Moving a record

    [Fact]
    public void SwitchWorkflow_ShouldMoveTheRecordOntoTheMappedStatus()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        var replacement = VersionWorkflow("New");
        var sut = VersionOn(old);
        var remap = StatusRemap.AutoMap(old, replacement).Value;

        // Act
        var result = sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now, "Annual workflow change.");

        // Assert
        // Same name, different workflow — so a different status id.
        result.IsSuccess.Should().BeTrue();
        sut.StatusName.Should().Be("Planned");
        sut.StatusId.Should().Be(replacement.Statuses.Single(s => s.Name == "Planned").Id);
    }

    [Fact]
    public void SwitchWorkflow_ShouldRecordTheMoveAsATransition()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        var replacement = VersionWorkflow("New");
        var sut = VersionOn(old);
        var remap = StatusRemap.AutoMap(old, replacement).Value;

        // Act
        sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now, "Annual workflow change.");

        // Assert
        // A switch is visible in the history rather than a silent rewrite: the transition carries the
        // old workflow's status and the new one's.
        var transition = sut.StatusTransitions.Last();
        transition.WorkflowId.Should().Be(replacement.Id);
        transition.FromStatusId.Should().Be(old.Statuses.Single(s => s.Name == "Planned").Id);
        transition.ToStatusId.Should().Be(replacement.Statuses.Single(s => s.Name == "Planned").Id);
        transition.Reason.Should().Be("Annual workflow change.");
    }

    [Fact]
    public void SwitchWorkflow_ShouldTranslateByAlias_WhenTheNewWorkflowRenamedTheStatus()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        var replacement = StatusWorkflow.Create("New", null, ProductWorkflowOwners.Version.Key).Value;
        replacement.AddStatus("Queued", null, StatusCategory.Proposed);
        replacement.AddStatus("Cut", null, StatusCategory.Active, (int)ProductStatusAlias.Ready);
        replacement.AddStatus("Shipped", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        replacement.AddStatus("Pulled", null, StatusCategory.Removed, (int)ProductStatusAlias.Withdrawn);

        var sut = VersionOn(old);
        sut.Cut(new LocalDate(2026, 9, 1),
            StatusRef.From(old.Statuses.Single(s => s.Name == "Ready")),
            ProductName, EventActor.System, _dateTimeProvider.Now);

        var remap = StatusRemap.AutoMap(old, replacement).Value;

        // Act
        var result = sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // "Ready" became "Cut", but both mean Ready, so the record lands correctly without anyone
        // deciding anything.
        result.IsSuccess.Should().BeTrue();
        sut.StatusName.Should().Be("Cut");
    }

    #endregion Moving a record

    #region Refusals

    [Fact]
    public void SwitchWorkflow_ShouldFail_WhenTheRemapIsIncomplete()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        old.AddStatus("On Hold", null, StatusCategory.Active);

        var replacement = VersionWorkflow("New");
        replacement.AddStatus("Paused", null, StatusCategory.Active);
        replacement.AddStatus("Deferred", null, StatusCategory.Active);

        var sut = VersionOn(old);
        var remap = StatusRemap.AutoMap(old, replacement).Value;

        // Act
        var result = sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The switch is validated, not repaired — a record is never left holding a status its workflow
        // does not contain.
        remap.IsComplete.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Every status must be mapped before records can be moved.");
    }

    [Fact]
    public void SwitchWorkflow_ShouldFail_ForARecordFromADifferentWorkflow()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        var replacement = VersionWorkflow("New");
        var unrelated = VersionWorkflow("Unrelated");

        var sut = VersionOn(unrelated);
        var remap = StatusRemap.AutoMap(old, replacement).Value;

        // Act
        var result = sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Guessing would strand it silently.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This record's status is not in the workflow being moved from.");
    }

    #endregion Refusals

    #region Resumability

    [Fact]
    public void SwitchWorkflow_ShouldBeANoOp_ForARecordAlreadyMoved()
    {
        // Arrange
        var old = VersionWorkflow("Old");
        var replacement = VersionWorkflow("New");
        var sut = VersionOn(old);
        var remap = StatusRemap.AutoMap(old, replacement).Value;

        sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now);
        var afterFirst = sut.StatusTransitionCount;

        // Act
        var result = sut.SwitchWorkflow(remap, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A large migration runs in batches and may be re-run after an interruption; re-running over a
        // record already moved must not record a second transition.
        result.IsSuccess.Should().BeTrue();
        sut.StatusTransitionCount.Should().Be(afterFirst);
    }

    #endregion Resumability
}
