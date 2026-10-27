using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeChannels;
using Wayd.DemandManagement.Application.IntakeChannels.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeChannels.Commands;

public sealed class RestoreIntakeChannelCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private RestoreIntakeChannelCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ForAnArchivedChannel_RestoresIt()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var channel = new IntakeChannelFaker().AsArchived().Generate();
        _scenario.DbContext.AddChannels(channel);

        // Act
        var result = await CreateHandler().Handle(new RestoreIntakeChannelCommand(channel.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        channel.IsActive.Should().BeTrue();
        channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelRestoredEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForAnUnknownChannel_FailsAsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new RestoreIntakeChannelCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeChannelErrors.NotFoundError);
    }
}
