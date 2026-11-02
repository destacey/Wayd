using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>Changing the workflow records follow when their type has no entry.</summary>
public sealed class ChangeWorkflowSchemeDefaultCommandHandlerTests : WorkflowSchemeHandlerTestBase
{
    private ChangeWorkflowSchemeDefaultCommandHandler CreateSut(params IRecordTypeStatusMigrator[] migrators) =>
        new(DbContext, migrators, CurrentUser.Object, Mock.Of<ICurrentPrincipal>(), DateTimeProvider.Object,
            Logger<ChangeWorkflowSchemeDefaultCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldMoveRecordsFollowingTheDefault_LeavingTypesWithAnEntryAlone()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var bugFlow = TicketWorkflow("Bug Flow");
        var lean = TicketWorkflow("Lean");
        var scheme = Scheme(standard);
        scheme.SetEntry(Bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        var scopeId = Guid.NewGuid();
        Assign(scheme, standard, scopeId);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new ChangeWorkflowSchemeDefaultCommand(scheme.Id, lean.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        scheme.DefaultWorkflowId.Should().Be(lean.Id);
        var (remap, selection) = Migrated.Should().ContainSingle().Subject;
        remap.FromWorkflowId.Should().Be(standard.Id);
        remap.ToWorkflowId.Should().Be(lean.Id);
        selection.RecordTypeId.Should().BeNull();
        selection.ExcludedRecordTypeIds.Should().BeEquivalentTo([Bug.Id]);
        selection.ScopeIds.Should().BeEquivalentTo(new Guid?[] { scopeId });
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheWorkflowIsAlreadyTheDefault()
    {
        // Arrange
        var standard = TicketWorkflow("Standard");
        var scheme = Scheme(standard);
        Assign(scheme, standard, null);

        // Act
        var result = await CreateSut(Migrator.Object).Handle(
            new ChangeWorkflowSchemeDefaultCommand(scheme.Id, standard.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Migrated.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
