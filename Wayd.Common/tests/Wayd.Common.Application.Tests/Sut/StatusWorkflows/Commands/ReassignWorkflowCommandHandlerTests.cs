using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Commands;

/// <summary>
/// Moving a scope onto another workflow. The migration itself is covered against SQL Server in
/// <c>ReassignWorkflowIntegrationTests</c>; this covers what is refused before anything moves.
/// </summary>
public sealed class ReassignWorkflowCommandHandlerTests : StatusWorkflowHandlerTestBase
{
    private readonly Mock<IStatusRecordMigrator> _migrator = new();

    private ReassignWorkflowCommandHandler CreateSut() =>
        new(DbContext, [_migrator.Object], CurrentUser.Object, Mock.Of<ICurrentPrincipal>(), DateTimeProvider.Object,
            Logger<ReassignWorkflowCommandHandler>(), OwnerAvailability);

    [Fact]
    public async Task Handle_ShouldReadAsNotFound_WhenTheAssignmentsFeatureIsOff()
    {
        // Arrange
        var current = SeedWorkflow("Current Gadget", publish: true, owner: Gadget);
        var target = SeedWorkflow("Target Gadget", publish: true, owner: Gadget);
        var assignment = WorkflowAssignment.Create(Gadget.Key, null, current, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);

        // Act
        var result = await CreateSut().Handle(
            new ReassignWorkflowCommand(assignment.Id, target.Id, []), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Workflow assignment not found.");
        assignment.WorkflowId.Should().Be(current.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
        _migrator.VerifyNoOtherCalls();
    }
}
