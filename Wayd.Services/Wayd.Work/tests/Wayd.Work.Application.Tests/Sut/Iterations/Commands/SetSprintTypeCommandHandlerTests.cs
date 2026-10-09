using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Tests.Infrastructure;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Commands;

public class SetSprintTypeCommandHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new();
    private readonly SetSprintTypeCommandHandler _handler;

    public SetSprintTypeCommandHandlerTests()
    {
        _handler = new SetSprintTypeCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<SetSprintTypeCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_ForATeamMember_SetsTheType()
    {
        // Act
        var result = await _handler.Handle(new SetSprintTypeCommand(_scenario.Sprint1.Id, SprintType.NonStandard), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.SprintTypeOverride.Should().Be(SprintType.NonStandard);
        _scenario.Sprint1.DomainEvents.OfType<SprintTypeSetEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoType_ClearsTheTeamsType()
    {
        // Arrange
        _scenario.Sprint1.SetSprintType(SprintType.NonStandard, EventActor.System, _scenario.DateTimeProvider.Now);
        _scenario.Sprint1.ClearDomainEvents();

        // Act
        var result = await _handler.Handle(new SetSprintTypeCommand(_scenario.Sprint1.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.SprintTypeOverride.Should().BeNull();
        _scenario.Sprint1.DomainEvents.OfType<SprintTypeClearedEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForSomeoneOutsideTheTeam_FailsWithoutSaving()
    {
        // Arrange
        _scenario.IsMember = false;

        // Act
        var result = await _handler.Handle(new SetSprintTypeCommand(_scenario.Sprint1.Id, SprintType.NonStandard), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint1.SprintTypeOverride.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnAdministratorOutsideTheTeam_SetsTheType()
    {
        // Arrange
        _scenario.IsMember = false;
        _scenario.IsAdministrator = true;

        // Act
        var result = await _handler.Handle(new SetSprintTypeCommand(_scenario.Sprint1.Id, SprintType.NonStandard), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.SprintTypeOverride.Should().Be(SprintType.NonStandard);
    }

    [Fact]
    public async Task Handle_ForAnUnknownSprint_FailsWithoutSaving()
    {
        // Act
        var result = await _handler.Handle(new SetSprintTypeCommand(Guid.NewGuid(), SprintType.NonStandard), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
