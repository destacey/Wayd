using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeChannels;
using Wayd.DemandManagement.Application.IntakeChannels.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeChannels.Commands;

public sealed class CreateIntakeChannelCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeChannelCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenValid_AddsTheChannelAfterTheLast()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        _scenario.DbContext.AddChannels(
            new IntakeChannelFaker().WithName("Support").WithOrder(1).Generate(),
            new IntakeChannelFaker().WithName("Security").WithOrder(4).AsArchived().Generate());

        // Act
        var result = await CreateHandler().Handle(new CreateIntakeChannelCommand(" Partner ", "From a partner."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var channel = _scenario.DbContext.IntakeChannels.Single(c => c.Id == result.Value);
        channel.Name.Should().Be("Partner");
        channel.Order.Should().Be(5);
        channel.IsActive.Should().BeTrue();
        channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelCreatedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithTheNameOfAnArchivedChannel_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        _scenario.DbContext.AddChannels(new IntakeChannelFaker().WithName("Support").AsArchived().Generate());

        // Act
        var result = await CreateHandler().Handle(new CreateIntakeChannelCommand("Support", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeChannelErrors.DuplicateNameError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
