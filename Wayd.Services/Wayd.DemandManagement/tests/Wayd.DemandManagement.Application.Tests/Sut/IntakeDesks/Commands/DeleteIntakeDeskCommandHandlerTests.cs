using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class DeleteIntakeDeskCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IScopedWorkflowAssignments> _assignments = new();

    public DeleteIntakeDeskCommandHandlerTests()
    {
        _assignments.Setup(a => a.RemoveScope(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<EventActor>(), It.IsAny<Instant>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private DeleteIntakeDeskCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _assignments.Object);

    [Fact]
    public async Task Handle_ForADeskWithNoRequests_RecordsTheDeletionAndRemovesIt()
    {
        // Arrange
        var desk = _scenario.Desk();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeDeskCommand(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        desk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeDeskDeletedEvent>();
        _scenario.DbContext.IntakeDesks.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
        // A workflow the desk chose goes with it, or it could never be archived.
        _assignments.Verify(a => a.RemoveScope(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, It.IsAny<EventActor>(),
            It.IsAny<Instant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForADeskWithRequests_FailsAndKeepsIt()
    {
        // Arrange — every request is on exactly one desk, so a desk with requests is closed instead
        var desk = _scenario.Desk();
        _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeDeskCommand(desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DeleteIntakeDeskCommandHandler.HasRequestsError);
        desk.DomainEvents.Should().BeEmpty();
        _scenario.DbContext.IntakeDesks.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
