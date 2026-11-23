using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectLifecycles.Commands;

public sealed class ActivateProjectLifecycleCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProjectLifecycleFaker _faker = new();

    private ActivateProjectLifecycleCommandHandler CreateHandler() =>
        new(_dbContext, Mock.Of<ILogger<ActivateProjectLifecycleCommandHandler>>());

    [Fact]
    public async Task Handle_ShouldActivateAProposedLifecycleWithStages()
    {
        // Arrange
        var lifecycle = _faker.AsProposedWithStages(("Plan", "Planning"), ("Execute", "Execution"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectLifecycleCommand(lifecycle.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        lifecycle.State.Should().Be(ProjectLifecycleState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheLifecycleHasNoStages()
    {
        // Arrange
        var lifecycle = _faker.AsProposedWithStages();
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectLifecycleCommand(lifecycle.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A lifecycle must have at least one stage before it can be activated.");
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheLifecycleIsNotProposed()
    {
        // Arrange
        var lifecycle = _faker.AsActiveWithStages(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectLifecycleCommand(lifecycle.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only proposed lifecycles can be activated.");
        lifecycle.State.Should().Be(ProjectLifecycleState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheLifecycleDoesNotExist()
    {
        // Arrange
        _dbContext.AddProjectLifecycle(_faker.AsProposedWithStages(("Plan", "Planning")));

        // Act
        var result = await CreateHandler().Handle(new ActivateProjectLifecycleCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Lifecycle not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
