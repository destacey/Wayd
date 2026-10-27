using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeChannels.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeChannels.Queries;

public sealed class GetIntakeChannelsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeChannelsQueryHandler CreateHandler() => new(_scenario.DbContext);

    [Fact]
    public async Task Handle_WithoutADesk_ReturnsActiveChannelsInOrder()
    {
        // Arrange
        var second = new IntakeChannelFaker().WithName("Support").WithOrder(2).Generate();
        var first = new IntakeChannelFaker().WithName("Security").WithOrder(1).Generate();
        var archived = new IntakeChannelFaker().WithName("Engineering").WithOrder(3).AsArchived().Generate();
        _scenario.DbContext.AddChannels(second, first, archived);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeChannelsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(c => c.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Handle_IncludingArchived_ReturnsEveryChannel()
    {
        // Arrange
        _scenario.DbContext.AddChannels(new IntakeChannelFaker().Generate(), new IntakeChannelFaker().AsArchived().Generate());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeChannelsQuery(IncludeArchived: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ForADeskOfferingEveryChannel_ReturnsEveryActiveChannel()
    {
        // Arrange
        var desk = _scenario.Desk();
        var active = new IntakeChannelFaker().Generate();
        _scenario.DbContext.AddChannels(active, new IntakeChannelFaker().AsArchived().Generate());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeChannelsQuery(IncludeArchived: true, DeskId: desk.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(c => c.Id).Should().Equal(active.Id);
    }

    [Fact]
    public async Task Handle_ForADeskThatChoseChannels_ReturnsOnlyItsActiveChosenOnes()
    {
        // Arrange
        var desk = _scenario.Desk();
        var chosen = new IntakeChannelFaker().Generate();
        var chosenButArchived = new IntakeChannelFaker().AsArchived().Generate();
        _scenario.DbContext.AddChannels(chosen, chosenButArchived, new IntakeChannelFaker().Generate());
        desk.ChangeChannels(false, [chosen.Id, chosenButArchived.Id], EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeChannelsQuery(DeskId: desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Select(c => c.Id).Should().Equal(chosen.Id);
    }

    [Fact]
    public async Task Handle_ForADeskOfferingNone_ReturnsNothing()
    {
        // Arrange
        var desk = _scenario.Desk();
        _scenario.DbContext.AddChannels(new IntakeChannelFaker().Generate());
        desk.ChangeChannels(false, [], EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeChannelsQuery(DeskId: desk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
