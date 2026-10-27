using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeChannels.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeChannels.Commands;

public sealed class ReorderIntakeChannelsCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ReorderIntakeChannelsCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WithEveryChannel_NumbersThemInOrderAndRecordsOnlyThoseMoved()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var first = new IntakeChannelFaker().WithOrder(1).Generate();
        var second = new IntakeChannelFaker().WithOrder(2).Generate();
        var archived = new IntakeChannelFaker().WithOrder(3).AsArchived().Generate();
        _scenario.DbContext.AddChannels(first, second, archived);

        // Act
        var result = await CreateHandler().Handle(new ReorderIntakeChannelsCommand([second.Id, first.Id, archived.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        second.Order.Should().Be(1);
        first.Order.Should().Be(2);
        archived.Order.Should().Be(3);
        second.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelReorderedEvent>();
        archived.DomainEvents.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LeavingAChannelOut_FailsWithoutMovingAny()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var first = new IntakeChannelFaker().WithOrder(1).Generate();
        var second = new IntakeChannelFaker().WithOrder(2).Generate();
        _scenario.DbContext.AddChannels(first, second, new IntakeChannelFaker().WithOrder(3).AsArchived().Generate());

        // Act
        var result = await CreateHandler().Handle(new ReorderIntakeChannelsCommand([second.Id, first.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The order must list every channel exactly once.");
        first.Order.Should().Be(1);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
