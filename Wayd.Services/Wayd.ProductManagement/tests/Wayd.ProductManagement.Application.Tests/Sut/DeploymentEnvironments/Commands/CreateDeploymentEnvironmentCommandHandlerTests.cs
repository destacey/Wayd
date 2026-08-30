using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Commands;

/// <summary>
/// Creating deployment targets.
/// </summary>
public sealed class CreateDeploymentEnvironmentCommandHandlerTests : ProductCommandTestBase
{
    private CreateDeploymentEnvironmentCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<CreateDeploymentEnvironmentCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCreateTheEnvironment()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateDeploymentEnvironmentCommand("Staging", EnvironmentCategory.Staging, 2),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = DbContext.DeploymentEnvironments.Should().ContainSingle().Subject;
        created.Name.Should().Be("Staging");
        created.Category.Should().Be(EnvironmentCategory.Staging);
    }

    [Fact]
    public async Task Handle_ShouldFail_OnADuplicateName()
    {
        // Arrange
        SeedEnvironment("Production");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateDeploymentEnvironmentCommand("Production", EnvironmentCategory.Production, 3),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An environment named 'Production' already exists.");
    }
}
