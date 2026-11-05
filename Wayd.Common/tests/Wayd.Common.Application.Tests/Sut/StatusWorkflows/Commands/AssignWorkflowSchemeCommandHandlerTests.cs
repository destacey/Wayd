using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Pointing a scope at another scheme of its owner type.</summary>
public sealed class AssignWorkflowSchemeCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private AssignWorkflowSchemeCommandHandler CreateSut(params IRecordTypeStatusMigrator[] migrators) =>
        new(DbContext, migrators, CurrentUser.Object, Mock.Of<ICurrentPrincipal>(), DateTimeProvider.Object,
            Logger<AssignWorkflowSchemeCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ToAClone_ShouldChangeOnlyThatScope_AndMoveNothing()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var scheme = Scheme(standard);
        scheme.SetEntry(Bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        var payments = Assign(scheme, standard, Guid.NewGuid());
        var platform = Assign(scheme, standard, Guid.NewGuid());
        var clone = scheme.Clone("Engineering (Payments)", null, EventActor.System, Now).Value;
        DbContext.AddStatusWorkflowScheme(clone);

        // Act
        var result = await CreateSut().Handle(
            new AssignWorkflowSchemeCommand(payments.Id, clone.Id, []), TestContext.Current.CancellationToken);

        // Assert
        // The clone sends every type where the original does, so no record changes workflow and no migrator is needed.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(0);
        payments.SchemeId.Should().Be(clone.Id);
        platform.SchemeId.Should().Be(scheme.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldMoveOnlyTheTypesWhoseWorkflowDiffers_InThatScope()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var current = Scheme(standard);
        var target = Scheme(standard, "Bug Triage");
        target.SetEntry(Bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        var scopeId = Guid.NewGuid();
        var assignment = Assign(current, standard, scopeId);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new AssignWorkflowSchemeCommand(assignment.Id, target.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        assignment.SchemeId.Should().Be(target.Id);
        var (remap, selection) = Migrated.Should().ContainSingle().Subject;
        remap.ToWorkflowId.Should().Be(bugFlow.Id);
        selection.RecordTypeId.Should().Be(Bug.Id);
        selection.ScopeIds.Should().BeEquivalentTo(new Guid?[] { scopeId });
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeRefused_ForAnOwnerTypeWithoutRecordTypes()
    {
        // Arrange
        var widgetWorkflow = SeedWorkflow("Widget", publish: true);
        var assignment = TestWorkflowAssignments.ForWorkflow(Widget.Key, null, widgetWorkflow, EventActor.System, Now).Value;
        DbContext.AddWorkflowAssignment(assignment);
        var standard = TicketWorkflow("Standard");
        var ticketScheme = Scheme(standard);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new AssignWorkflowSchemeCommand(assignment.Id, ticketScheme.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("do not have record types");
        assignment.SchemeId.Should().NotBe(ticketScheme.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
