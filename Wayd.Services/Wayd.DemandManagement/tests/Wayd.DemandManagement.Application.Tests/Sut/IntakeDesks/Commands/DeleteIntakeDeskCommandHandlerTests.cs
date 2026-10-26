using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Commands;

public sealed class DeleteIntakeDeskCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private DeleteIntakeDeskCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

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
