using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Commands;

/// <summary>
/// Retiring an environment. Deployments already recorded against it stand as history.
/// </summary>
public sealed class SetDeploymentEnvironmentActiveCommandHandlerTests : ProductCommandTestBase
{
    private SetDeploymentEnvironmentActiveCommandHandler ActivationSut() =>
        new(DbContext, CurrentUser.Object, Logger<SetDeploymentEnvironmentActiveCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldDeactivateTheEnvironment()
    {
        // Arrange
        var environment = SeedEnvironment();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetDeploymentEnvironmentActiveCommand(environment.Id, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        environment.IsActive.Should().BeFalse();

        environment.ExecutePostPersistenceActions();
        var retired = environment.DomainEvents.OfType<EnvironmentRetiredEventV2>().Should().ContainSingle().Subject;
        retired.Actor.UserId.Should().Be(CurrentUser.Object.GetUserId());
        retired.Timestamp.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldReactivateTheEnvironmentAndRaiseAnEvent()
    {
        // Arrange
        var environment = SeedEnvironment();
        environment.Deactivate(EventActor.System, Now);
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetDeploymentEnvironmentActiveCommand(environment.Id, true), TestContext.Current.CancellationToken);
        environment.ExecutePostPersistenceActions();

        // Assert
        result.IsSuccess.Should().BeTrue();
        environment.IsActive.Should().BeTrue();

        var reinstated = environment.DomainEvents.OfType<EnvironmentReinstatedEvent>().Should().ContainSingle().Subject;
        reinstated.Actor.UserId.Should().Be(CurrentUser.Object.GetUserId());
        reinstated.Timestamp.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAlreadyActive()
    {
        // Arrange
        var environment = SeedEnvironment();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetDeploymentEnvironmentActiveCommand(environment.Id, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
