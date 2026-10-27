using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeChannels;
using Wayd.DemandManagement.Application.IntakeChannels.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeChannels.Commands;

public sealed class UpdateIntakeChannelCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UpdateIntakeChannelCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenRenamed_RecordsTheChange()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var channel = new IntakeChannelFaker().WithName("Support").Generate();
        _scenario.DbContext.AddChannels(channel);

        // Act
        var result = await CreateHandler().Handle(new UpdateIntakeChannelCommand(channel.Id, "Customer support", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        channel.Name.Should().Be("Customer support");
        channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelDetailsUpdatedEvent>()
            .Which.PreviousName.Should().Be("Support");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeepingItsOwnName_Succeeds()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var channel = new IntakeChannelFaker().WithName("Support").Generate();
        _scenario.DbContext.AddChannels(channel);

        // Act
        var result = await CreateHandler().Handle(new UpdateIntakeChannelCommand(channel.Id, "Support", "Support cases."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        channel.Description.Should().Be("Support cases.");
    }

    [Fact]
    public async Task Handle_WithAnotherChannelsName_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var channel = new IntakeChannelFaker().WithName("Support").Generate();
        _scenario.DbContext.AddChannels(channel, new IntakeChannelFaker().WithName("Security").Generate());

        // Act
        var result = await CreateHandler().Handle(new UpdateIntakeChannelCommand(channel.Id, "Security", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeChannelErrors.DuplicateNameError);
        channel.Name.Should().Be("Support");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnUnknownChannel_FailsAsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new UpdateIntakeChannelCommand(Guid.NewGuid(), "Support", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeChannelErrors.NotFoundError);
    }
}
