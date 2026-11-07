using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.FeatureManagement.Commands;

public sealed class ToggleFeatureFlagCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IFeatureFlagCacheInvalidator> _cacheInvalidator = new();
    private readonly FeatureFlagFaker _faker = new();

    private ToggleFeatureFlagCommandHandler CreateHandler() =>
        new(_dbContext, _cacheInvalidator.Object, NullLogger<ToggleFeatureFlagCommandHandler>.Instance);

    [Fact]
    public async Task Handle_TurnsTheFlagOn_AndClearsTheCache()
    {
        // Arrange
        var flag = _faker.WithId(5).AsDisabled().Generate();
        _dbContext.AddFeatureFlag(flag);

        // Act
        var result = await CreateHandler().Handle(new ToggleFeatureFlagCommand(5, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _cacheInvalidator.Verify(c => c.InvalidateCache(), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsAnArchivedFlag()
    {
        // Arrange
        var flag = _faker.WithId(5).AsArchived().Generate();
        _dbContext.AddFeatureFlag(flag);

        // Act
        var result = await CreateHandler().Handle(new ToggleFeatureFlagCommand(5, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _cacheInvalidator.Verify(c => c.InvalidateCache(), Times.Never);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheFlagDoesNotExist()
    {
        // Act
        var result = await CreateHandler().Handle(new ToggleFeatureFlagCommand(99, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
