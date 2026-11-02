using Wayd.Tests.Shared.Extensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Changing which workflow one record type follows under a scheme.</summary>
public sealed class SetWorkflowSchemeEntryCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private SetWorkflowSchemeEntryCommandHandler CreateSut(params IRecordTypeStatusMigrator[] migrators) =>
        new(DbContext, migrators, CurrentUser.Object, Mock.Of<ICurrentPrincipal>(), DateTimeProvider.Object,
            Logger<SetWorkflowSchemeEntryCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldChangeTheEntryWithoutAMigrator_WhenNoScopeFollowsTheScheme()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);

        // Act
        var result = await CreateSut().Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, Bug.Id, bugFlow.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(0);
        scheme.WorkflowFor(Bug.Id).Should().Be(bugFlow.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeRefused_WhenTheSchemeIsInUseAndNothingCanMoveItsRecords()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        Assign(scheme, standard, Guid.NewGuid());

        // Act
        var result = await CreateSut().Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, Bug.Id, bugFlow.Id, []), TestContext.Current.CancellationToken);

        // Assert
        // Changing the scheme alone would leave every Bug holding a status its workflow does not contain.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be moved between workflows by record type yet");
        scheme.WorkflowFor(Bug.Id).Should().Be(standard.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMoveOnlyThatTypesRecords_InEveryScopeOnTheScheme()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        var (payments, platform) = (Guid.NewGuid(), Guid.NewGuid());
        Assign(scheme, standard, payments);
        Assign(scheme, standard, platform);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, Bug.Id, bugFlow.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(3);
        var (remap, selection) = Migrated.Should().ContainSingle().Subject;
        remap.FromWorkflowId.Should().Be(standard.Id);
        remap.ToWorkflowId.Should().Be(bugFlow.Id);
        selection.RecordTypeId.Should().Be(Bug.Id);
        selection.ScopeIds.Should().BeEquivalentTo(new Guid?[] { payments, platform });
        scheme.WorkflowFor(Bug.Id).Should().Be(bugFlow.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoWorkflow_ShouldRemoveTheEntry_MovingTheTypeBackToTheDefault()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        scheme.SetEntry(Bug, bugFlow, Remap(standard, bugFlow), Domain.Events.EventActor.System, Now);
        Assign(scheme, standard, null);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, Bug.Id, null, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        scheme.Entries.Should().BeEmpty();
        var (remap, selection) = Migrated.Should().ContainSingle().Subject;
        remap.FromWorkflowId.Should().Be(bugFlow.Id);
        remap.ToWorkflowId.Should().Be(standard.Id);
        selection.ScopeIds.Should().BeEquivalentTo(new Guid?[] { null });
    }

    [Fact]
    public async Task Handle_ShouldMoveNothing_WhenTheTargetWorkflowIsNotPublished()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var archived = TicketWorkflow("Retired Flow");
        archived.Archive(isAssigned: false, Domain.Events.EventActor.System, Now).IsSuccess.Should().BeTrue();
        var scheme = Scheme(standard);
        Assign(scheme, standard, Guid.NewGuid());

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, Bug.Id, archived.Id, []), TestContext.Current.CancellationToken);

        // Assert
        // Refused before the migrator runs, so no record is left on an archived workflow.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("'Retired Flow' is not published, so records cannot be moved onto it.");
        Migrated.Should().BeEmpty();
        scheme.WorkflowFor(Bug.Id).Should().Be(standard.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMoveNothing_WhenTheSchemeRefusesTheChange()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        Assign(scheme, standard, Guid.NewGuid());
        var widgetType = Domain.StatusWorkflows.RecordType.Create(Ticket.Key, "Gizmo", Domain.Events.EventActor.System, Now).Value;
        widgetType.SetPrivate(r => r.OwnerType, Widget.Key);
        DbContext.AddRecordType(widgetType);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new SetWorkflowSchemeEntryCommand(scheme.Id, widgetType.Id, bugFlow.Id, []), TestContext.Current.CancellationToken);

        // Assert
        // The scheme refuses a type of another owner type; records must not have moved by then.
        result.IsFailure.Should().BeTrue();
        Migrated.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
