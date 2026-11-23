using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectLifecycles.Commands;

public sealed class ArchiveProjectLifecycleCommandHandlerTests : IDisposable
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly ProjectLifecycleFaker _faker = new();

    private ArchiveProjectLifecycleCommandHandler CreateHandler() =>
        new(_dbContext, Mock.Of<ILogger<ArchiveProjectLifecycleCommandHandler>>());

    [Fact]
    public async Task Handle_ShouldArchiveAnActiveLifecycle()
    {
        // Arrange
        var lifecycle = _faker.AsActiveWithStages(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectLifecycleCommand(lifecycle.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        lifecycle.State.Should().Be(ProjectLifecycleState.Archived);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAndSaveNothing_WhenTheLifecycleIsNotActive()
    {
        // Arrange
        var lifecycle = _faker.AsProposedWithStages(("Plan", "Planning"));
        _dbContext.AddProjectLifecycle(lifecycle);

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectLifecycleCommand(lifecycle.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only active lifecycles can be archived.");
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheLifecycleDoesNotExist()
    {
        // Arrange
        _dbContext.AddProjectLifecycle(_faker.AsActiveWithStages(("Plan", "Planning")));

        // Act
        var result = await CreateHandler().Handle(new ArchiveProjectLifecycleCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Project Lifecycle not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
